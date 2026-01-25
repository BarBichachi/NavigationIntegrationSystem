using NavigationIntegrationSystem.Devices.Config.Connections;
using NavigationIntegrationSystem.Devices.Config.Enums;

namespace NavigationIntegrationSystem.Devices.Config;

// Holds persisted connection settings for a device (all sections are persisted; Kind selects the active one)
public sealed class DeviceConnectionSettings
{
    #region Properties
    public DeviceConnectionKind Kind { get; set; } = DeviceConnectionKind.Udp;

    public UdpConnectionSettings Udp { get; set; } = new UdpConnectionSettings();
    public TcpConnectionSettings Tcp { get; set; } = new TcpConnectionSettings();
    public SerialConnectionSettings Serial { get; set; } = new SerialConnectionSettings();
    #endregion

    #region Functions
    // Copies values from another connection settings instance (deep)
    public void CopyFrom(DeviceConnectionSettings i_Source)
    {
        if (i_Source == null) { return; }

        Kind = i_Source.Kind;

        if (Udp == null) { Udp = new UdpConnectionSettings(); }
        if (Tcp == null) { Tcp = new TcpConnectionSettings(); }
        if (Serial == null) { Serial = new SerialConnectionSettings(); }

        Udp.CopyFrom(i_Source.Udp);
        Tcp.CopyFrom(i_Source.Tcp);
        Serial.CopyFrom(i_Source.Serial);
    }

    // Creates a deep clone of this instance
    public DeviceConnectionSettings DeepClone()
    {
        var clone = new DeviceConnectionSettings();
        clone.CopyFrom(this);
        return clone;
    }
    #endregion
}