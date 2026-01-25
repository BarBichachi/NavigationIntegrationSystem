using NavigationIntegrationSystem.Devices.Config.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NavigationIntegrationSystem.Devices.Config.Validation;

// Validates connection settings for a device and returns user-friendly errors
public static class ConnectionSettingsValidator
{
    #region Functions
    // Validates a DeviceConnectionSettings instance and returns a list of validation errors
    public static IReadOnlyList<string> Validate(DeviceConnectionSettings i_Settings)
    {
        var errors = new List<string>();

        if (i_Settings == null) { errors.Add("Connection settings are missing"); return errors; }

        switch (i_Settings.Kind)
        {
            case DeviceConnectionKind.Udp:
                ValidateUdp(i_Settings, errors);
                break;

            case DeviceConnectionKind.Tcp:
                ValidateTcp(i_Settings, errors);
                break;

            case DeviceConnectionKind.Serial:
                ValidateSerial(i_Settings, errors);
                break;

            default:
                errors.Add("Unsupported connection kind");
                break;
        }

        return errors;
    }
    #endregion

    #region Private Functions
    // Validates UDP settings
    private static void ValidateUdp(DeviceConnectionSettings i_Settings, List<string> io_Errors)
    {
        if (i_Settings.Udp == null) { io_Errors.Add("UDP settings are missing"); return; }

        if (!IsIpValid(i_Settings.Udp.RemoteIp)) { io_Errors.Add($"UDP remote IP is invalid: '{i_Settings.Udp.RemoteIp}'"); }
        if (!IsIpValid(i_Settings.Udp.LocalIp)) { io_Errors.Add($"UDP local IP is invalid: '{i_Settings.Udp.LocalIp}'"); }

        if (!IsPortValid(i_Settings.Udp.RemotePort)) { io_Errors.Add($"UDP remote port must be between 1 and 65535: {i_Settings.Udp.RemotePort}"); }
        if (!IsPortValid(i_Settings.Udp.LocalPort)) { io_Errors.Add($"UDP local port must be between 1 and 65535: {i_Settings.Udp.LocalPort}"); }
    }

    // Validates TCP settings
    private static void ValidateTcp(DeviceConnectionSettings i_Settings, List<string> io_Errors)
    {
        if (i_Settings.Tcp == null) { io_Errors.Add("TCP settings are missing"); return; }

        if (string.IsNullOrWhiteSpace(i_Settings.Tcp.Host)) { io_Errors.Add($"TCP host is required: '{i_Settings.Tcp.Host}'"); }
        if (!IsPortValid(i_Settings.Tcp.Port)) { io_Errors.Add($"TCP port must be between 1 and 65535: {i_Settings.Tcp.Port}"); }
    }

    // Validates Serial settings
    private static void ValidateSerial(DeviceConnectionSettings i_Settings, List<string> io_Errors)
    {
        if (i_Settings.Serial == null) { io_Errors.Add("Serial settings are missing"); return; }

        if (string.IsNullOrWhiteSpace(i_Settings.Serial.ComPort)) { io_Errors.Add($"Serial COM port is required: '{i_Settings.Serial.ComPort}'"); }
        if (i_Settings.Serial.BaudRate <= 0) { io_Errors.Add($"Serial baud rate must be greater than 0: {i_Settings.Serial.BaudRate}"); }
    }

    // Checks whether an integer is a valid TCP/UDP port number
    private static bool IsPortValid(int i_Port)
    {
        return i_Port >= 1 && i_Port <= 65535;
    }

    // Checks whether a string is a valid IP address
    private static bool IsIpValid(string i_Ip)
    {
        if (string.IsNullOrWhiteSpace(i_Ip)) { return false; }
        return IPAddress.TryParse(i_Ip, out _);
    }

    // TODO - Start from here!
    // Validates either an IP address or a hostname (rejects partial IPs like 127.0.0)
    private static bool IsHostOrIpValid(string i_Host)
    {
        if (string.IsNullOrWhiteSpace(i_Host)) { return false; }

        string host = i_Host.Trim();

        // If it looks like an IP literal (digits/dots/colons only), require full IP parse
        bool looksLikeIp = host.All(c => char.IsDigit(c) || c == '.' || c == ':');
        if (looksLikeIp) { return IPAddress.TryParse(host, out _); }

        // Otherwise treat as hostname
        return Uri.CheckHostName(host) != UriHostNameType.Unknown;
    }

    #endregion
}
