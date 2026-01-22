using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Devices.Config;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NavigationIntegrationSystem.Infrastructure.Persistence.DevicesConfig;

// Loads and saves devices.json configuration for the fixed device instances
public sealed class DevicesConfigService
{
    #region Private Fields
    private readonly string m_ConfigFilePath;
    private readonly JsonSerializerOptions m_JsonOptions;
    #endregion

    #region Ctors
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
        return config ?? new DevicesConfigFile();
    }

    // Saves devices configuration to disk
    public void Save(DevicesConfigFile i_Config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(m_ConfigFilePath)!);
        string json = JsonSerializer.Serialize(i_Config, m_JsonOptions);
        File.WriteAllText(m_ConfigFilePath, json);
    }

    // Gets an existing config for deviceId or creates a default one
    public DeviceConfig GetOrCreateDevice(DevicesConfigFile i_Config, DeviceType i_DeviceType)
    {
        DeviceConfig? existing = i_Config.Devices.FirstOrDefault(d => d.DeviceType == i_DeviceType);
        if (existing != null) { return existing; }

        var created = new DeviceConfig { DeviceType = i_DeviceType, AutoReconnect = true, Connection = new DeviceConnectionConfig() };
        i_Config.Devices.Add(created);
        return created;
    }
    #endregion
}
