using NavigationIntegrationSystem.Devices.Config.Connections;
using NavigationIntegrationSystem.Devices.Config.Enums;
using System.Text.Json.Serialization;

namespace NavigationIntegrationSystem.Devices.Config;

// Holds persisted connection parameters for a device using a discriminator model (only relevant section is saved)
public sealed class DeviceConnectionConfig
{
    #region Private Fields
    private UdpConnectionConfig m_Udp = new UdpConnectionConfig();
    private TcpConnectionConfig m_Tcp = new TcpConnectionConfig();
    private SerialConnectionConfig m_Serial = new SerialConnectionConfig();
    #endregion

    #region Properties
    public DeviceConnectionKind Kind { get; set; } = DeviceConnectionKind.Udp;

    // Runtime always-available sections (not persisted directly)
    [JsonIgnore] public UdpConnectionConfig Udp { get => m_Udp; set => m_Udp = value ?? new UdpConnectionConfig(); }
    [JsonIgnore] public TcpConnectionConfig Tcp { get => m_Tcp; set => m_Tcp = value ?? new TcpConnectionConfig(); }
    [JsonIgnore] public SerialConnectionConfig Serial { get => m_Serial; set => m_Serial = value ?? new SerialConnectionConfig(); }

    // Persisted (only one is non-null depending on Kind)
    [JsonPropertyName("Udp")] public UdpConnectionConfig? ActiveUdp { get => Kind == DeviceConnectionKind.Udp ? m_Udp : null; set { if (value != null) { m_Udp = value; } } }
    [JsonPropertyName("Tcp")] public TcpConnectionConfig? ActiveTcp { get => Kind == DeviceConnectionKind.Tcp ? m_Tcp : null; set { if (value != null) { m_Tcp = value; } } }
    [JsonPropertyName("Serial")] public SerialConnectionConfig? ActiveSerial { get => Kind == DeviceConnectionKind.Serial ? m_Serial : null; set { if (value != null) { m_Serial = value; } } }
    #endregion

    #region Functions
    // Copies values from another connection config (deep)
    public void CopyFrom(DeviceConnectionConfig i_Source)
    {
        if (i_Source == null) { return; }

        Kind = i_Source.Kind;

        Udp.CopyFrom(i_Source.Udp);
        Tcp.CopyFrom(i_Source.Tcp);
        Serial.CopyFrom(i_Source.Serial);
    }

    // Creates a deep clone of this config
    public DeviceConnectionConfig DeepClone()
    {
        var clone = new DeviceConnectionConfig();
        clone.Kind = Kind;

        clone.Udp = Udp.DeepClone();
        clone.Tcp = Tcp.DeepClone();
        clone.Serial = Serial.DeepClone();

        return clone;
    }
    #endregion
}