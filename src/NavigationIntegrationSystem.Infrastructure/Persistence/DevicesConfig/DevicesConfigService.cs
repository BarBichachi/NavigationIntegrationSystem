using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Devices.Connections;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.Devices.Validation;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Infrastructure.Persistence.DevicesConfig;

// Loads and saves devices.json configuration for the fixed device instances
public sealed class DevicesConfigService
{
    #region Private Fields
    private readonly string m_ConfigFilePath;
    private readonly JsonSerializerOptions m_JsonOptions;
    #endregion

    #region Ctors
    // Creates a devices configuration service for the specified path
    public DevicesConfigService(string i_ConfigFilePath)
    {
        m_ConfigFilePath = i_ConfigFilePath;

        m_JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }
    #endregion

    #region Functions
    // Loads devices configuration from disk or returns an empty config if missing
    public DevicesConfigFile Load()
    {
        if (!File.Exists(m_ConfigFilePath)) { return new DevicesConfigFile(); }

        string json = File.ReadAllText(m_ConfigFilePath);
        DevicesConfigFile? config = JsonSerializer.Deserialize<DevicesConfigFile>(json, m_JsonOptions);
        DevicesConfigFile resolved = config ?? new DevicesConfigFile();
        EnsureConnectionDefaults(resolved);
        return resolved;
    }

    // Saves devices configuration to disk
    public void Save(DevicesConfigFile i_Config)
    {
        SaveToFile(i_Config, m_ConfigFilePath);
    }

    // Saves devices configuration to disk with a result
    public DevicesConfigExportResult SaveWithResult(DevicesConfigFile i_Config)
    {
        return ExportToFile(i_Config, m_ConfigFilePath);
    }

    // Imports devices configuration from a file with validation
    public DevicesConfigImportResult ImportFromFile(string i_FilePath, IEnumerable<DeviceType> i_ExpectedDeviceTypes, IEnumerable<int> i_AllowedPlaybackFrequencies)
    {
        if (!File.Exists(i_FilePath)) { return DevicesConfigImportResult.Failure("The selected file does not exist."); }

        string json = File.ReadAllText(i_FilePath);
        DevicesConfigFile? imported = JsonSerializer.Deserialize<DevicesConfigFile>(json, m_JsonOptions);
        if (imported == null) { return DevicesConfigImportResult.Failure("The selected file does not contain valid devices configuration."); }

        string? validationError = ValidateImportedConfig(imported, i_ExpectedDeviceTypes, i_AllowedPlaybackFrequencies);
        if (!string.IsNullOrEmpty(validationError)) { return DevicesConfigImportResult.Failure(validationError); }

        EnsureConnectionDefaults(imported);
        return DevicesConfigImportResult.Success(imported);
    }

    // Loads devices configuration from an arbitrary file path
    public DevicesConfigFile? LoadFromFile(string i_FilePath)
    {
        if (!File.Exists(i_FilePath)) { return null; }

        string json = File.ReadAllText(i_FilePath);
        return JsonSerializer.Deserialize<DevicesConfigFile>(json, m_JsonOptions);
    }

    // Exports devices configuration to an arbitrary file path
    public DevicesConfigExportResult ExportToFile(DevicesConfigFile i_Config, string i_FilePath)
    {
        try
        {
            SaveToFile(i_Config, i_FilePath);
            return DevicesConfigExportResult.Success();
        }
        catch (System.Exception ex)
        {
            return DevicesConfigExportResult.Failure(ex.Message);
        }
    }

    // Saves devices configuration to an arbitrary file path
    public void SaveToFile(DevicesConfigFile i_Config, string i_FilePath)
    {
        DevicesConfigFile sanitized = CreateSanitizedConfig(i_Config);
        Directory.CreateDirectory(Path.GetDirectoryName(i_FilePath)!);
        string json = JsonSerializer.Serialize(sanitized, m_JsonOptions);
        File.WriteAllText(i_FilePath, json);
    }

    // Gets an existing config for deviceId or creates a default one
    public DeviceConfig GetOrCreateDevice(DevicesConfigFile i_Config, DeviceType i_DeviceType)
    {
        DeviceConfig? existing = i_Config.Devices.FirstOrDefault(d => d.DeviceType == i_DeviceType);
        if (existing != null)
        {
            EnsureConnectionDefaults(existing);
            return existing;
        }

        DeviceConfig created = new DeviceConfig { DeviceType = i_DeviceType, AutoReconnect = true, Connection = new DeviceConnectionSettings() };
        EnsureConnectionDefaults(created);
        i_Config.Devices.Add(created);
        return created;
    }

    // Applies imported device configs into an existing config file
    public void ApplyImportedConfig(DevicesConfigFile i_Target, DevicesConfigFile i_Imported)
    {
        foreach (DeviceConfig imported in i_Imported.Devices)
        {
            EnsureConnectionDefaults(imported);
            DeviceConfig target = GetOrCreateDevice(i_Target, imported.DeviceType);
            target.CopyFrom(imported);
        }
    }

    // Ensures every device has all connection sections initialized
    private static void EnsureConnectionDefaults(DevicesConfigFile i_Config)
    {
        foreach (DeviceConfig device in i_Config.Devices)
        {
            EnsureConnectionDefaults(device);
        }
    }

    // Ensures a device has all connection sections initialized
    private static void EnsureConnectionDefaults(DeviceConfig i_Device)
    {
        if (i_Device.Connection == null) { i_Device.Connection = new DeviceConnectionSettings(); }
        if (i_Device.Connection.Udp == null) { i_Device.Connection.Udp = new UdpConnectionSettings(); }
        if (i_Device.Connection.Tcp == null) { i_Device.Connection.Tcp = new TcpConnectionSettings(); }
        if (i_Device.Connection.Serial == null) { i_Device.Connection.Serial = new SerialConnectionSettings(); }
        if (i_Device.Connection.Playback == null) { i_Device.Connection.Playback = new PlaybackConnectionSettings(); }
    }

    // Validates imported device configuration for consistency
    private static string? ValidateImportedConfig(DevicesConfigFile i_Config, IEnumerable<DeviceType> i_ExpectedDeviceTypes, IEnumerable<int> i_AllowedPlaybackFrequencies)
    {
        List<DeviceType> expectedTypes = i_ExpectedDeviceTypes
            .Where(type => type != DeviceType.Manual)
            .Distinct()
            .ToList();

        List<DeviceType> importedTypes = i_Config.Devices
            .Select(device => device.DeviceType)
            .Distinct()
            .ToList();

        if (i_Config.Devices.Count != importedTypes.Count)
        {
            return "The imported file contains duplicate device entries.";
        }

        List<DeviceType> manualEntries = i_Config.Devices
            .Where(device => device.DeviceType == DeviceType.Manual)
            .Select(device => device.DeviceType)
            .ToList();

        if (manualEntries.Count > 0)
        {
            return "The imported file contains Manual device entries. Manual devices must be omitted.";
        }

        List<DeviceType> unknownTypes = importedTypes
            .Where(type => !expectedTypes.Contains(type))
            .ToList();

        if (unknownTypes.Count > 0)
        {
            string unknownList = string.Join(", ", unknownTypes);
            return $"The imported file contains unsupported device types: {unknownList}.";
        }

        List<DeviceType> missingTypes = expectedTypes
            .Where(type => !importedTypes.Contains(type))
            .ToList();

        if (missingTypes.Count > 0)
        {
            string missingList = string.Join(", ", missingTypes);
            return $"The imported file is missing device entries for: {missingList}.";
        }

        foreach (DeviceConfig device in i_Config.Devices)
        {
            IReadOnlyList<string> errors = ConnectionSettingsValidator.Validate(device.Connection, device.DeviceType);
            if (errors.Count > 0)
            {
                return string.Join("\n", errors);
            }

            if (device.DeviceType == DeviceType.Playback && device.Connection?.Playback != null)
            {
                if (!i_AllowedPlaybackFrequencies.Contains(device.Connection.Playback.Frequency))
                {
                    return "The imported file contains an unsupported playback frequency.";
                }
            }
        }

        return null;
    }


    // Creates a sanitized config for persistence
    private static DevicesConfigFile CreateSanitizedConfig(DevicesConfigFile i_Config)
    {
        DevicesConfigFile sanitized = new DevicesConfigFile();

        foreach (DeviceConfig device in i_Config.Devices)
        {
            if (device.DeviceType == DeviceType.Manual) { continue; }

            DeviceConfig clone = device.DeepClone();
            SanitizeDeviceConnection(clone);
            sanitized.Devices.Add(clone);
        }

        return sanitized;
    }

    // Removes irrelevant connection sections based on device type
    private static void SanitizeDeviceConnection(DeviceConfig i_Device)
    {
        if (i_Device.Connection == null) { return; }

        if (i_Device.DeviceType == DeviceType.Playback)
        {
            i_Device.Connection.Udp = null;
            i_Device.Connection.Tcp = null;
            i_Device.Connection.Serial = null;
            return;
        }

        i_Device.Connection.Playback = null;
    }
    #endregion
}
