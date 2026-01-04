using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace NavigationIntegrationSystem.Infrastructure.Configuration.Devices;

// Holds persisted connection parameters for a device using a discriminator model (only relevant section is saved)
public sealed class DeviceConnectionConfig : ObservableObject
{
    #region Private Fields
    private DeviceConnectionKind m_Kind = DeviceConnectionKind.Udp;
    private UdpConnectionConfig m_Udp = new UdpConnectionConfig();
    private TcpConnectionConfig m_Tcp = new TcpConnectionConfig();
    private SerialConnectionConfig m_Serial = new SerialConnectionConfig();
    #endregion

    #region Properties
    public DeviceConnectionKind Kind
    {
        get => m_Kind;
        set
        {
            if (!SetProperty(ref m_Kind, value))
            { return; }
            OnPropertyChanged(nameof(Udp));
            OnPropertyChanged(nameof(Tcp));
            OnPropertyChanged(nameof(Serial));
            OnPropertyChanged(nameof(ActiveUdp));
            OnPropertyChanged(nameof(ActiveTcp));
            OnPropertyChanged(nameof(ActiveSerial));
        }
    }

    // Runtime always-available (for UI bindings)
    [JsonIgnore] public UdpConnectionConfig Udp { get => m_Udp; set => SetProperty(ref m_Udp, value); }
    [JsonIgnore] public TcpConnectionConfig Tcp { get => m_Tcp; set => SetProperty(ref m_Tcp, value); }
    [JsonIgnore] public SerialConnectionConfig Serial { get => m_Serial; set => SetProperty(ref m_Serial, value); }

    // Persisted (only one is non-null depending on Kind)
    [JsonPropertyName("Udp")] public UdpConnectionConfig? ActiveUdp { get => Kind == DeviceConnectionKind.Udp ? m_Udp : null; set { if (value != null) { m_Udp = value; OnPropertyChanged(nameof(Udp)); } } }
    [JsonPropertyName("Tcp")] public TcpConnectionConfig? ActiveTcp { get => Kind == DeviceConnectionKind.Tcp ? m_Tcp : null; set { if (value != null) { m_Tcp = value; OnPropertyChanged(nameof(Tcp)); } } }
    [JsonPropertyName("Serial")] public SerialConnectionConfig? ActiveSerial { get => Kind == DeviceConnectionKind.Serial ? m_Serial : null; set { if (value != null) { m_Serial = value; OnPropertyChanged(nameof(Serial)); } } }
    #endregion

    #region Functions
    // Copies values from another connection config (deep)
    public void CopyFrom(DeviceConnectionConfig i_Source)
    {
        if (i_Source == null)
        { return; }

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

// UDP connection parameters
public sealed class UdpConnectionConfig : ObservableObject
{
    #region Private Fields
    private string m_RemoteIp = "127.0.0.1";
    private int m_RemotePort = 5000;
    private string m_LocalIp = "0.0.0.0";
    private int m_LocalPort = 5000;
    #endregion

    #region Properties
    public string RemoteIp { get => m_RemoteIp; set => SetProperty(ref m_RemoteIp, value); }
    public int RemotePort { get => m_RemotePort; set => SetProperty(ref m_RemotePort, value); }
    public string LocalIp { get => m_LocalIp; set => SetProperty(ref m_LocalIp, value); }
    public int LocalPort { get => m_LocalPort; set => SetProperty(ref m_LocalPort, value); }
    #endregion

    #region Functions
    // Copies values from another instance
    public void CopyFrom(UdpConnectionConfig i_Source)
    {
        if (i_Source == null)
        { return; }
        RemoteIp = i_Source.RemoteIp;
        RemotePort = i_Source.RemotePort;
        LocalIp = i_Source.LocalIp;
        LocalPort = i_Source.LocalPort;
    }

    // Creates a deep clone of this instance
    public UdpConnectionConfig DeepClone()
    {
        var clone = new UdpConnectionConfig();
        clone.CopyFrom(this);
        return clone;
    }
    #endregion
}

// TCP connection parameters
public sealed class TcpConnectionConfig : ObservableObject
{
    #region Private Fields
    private string m_Host = "127.0.0.1";
    private int m_Port = 5000;
    #endregion

    #region Properties
    public string Host { get => m_Host; set => SetProperty(ref m_Host, value); }
    public int Port { get => m_Port; set => SetProperty(ref m_Port, value); }
    #endregion

    #region Functions
    // Copies values from another instance
    public void CopyFrom(TcpConnectionConfig i_Source)
    {
        if (i_Source == null)
        { return; }
        Host = i_Source.Host;
        Port = i_Source.Port;
    }

    // Creates a deep clone of this instance
    public TcpConnectionConfig DeepClone()
    {
        var clone = new TcpConnectionConfig();
        clone.CopyFrom(this);
        return clone;
    }
    #endregion
}

// Serial connection parameters
public sealed class SerialConnectionConfig : ObservableObject
{
    #region Private Fields
    private SerialLineKind m_SerialLineKind = SerialLineKind.Rs232;
    private string m_ComPort = "COM1";
    private int m_BaudRate = 115200;
    #endregion

    #region Properties
    public SerialLineKind SerialLineKind { get => m_SerialLineKind; set => SetProperty(ref m_SerialLineKind, value); }
    public string ComPort { get => m_ComPort; set => SetProperty(ref m_ComPort, value); }
    public int BaudRate { get => m_BaudRate; set => SetProperty(ref m_BaudRate, value); }
    #endregion

    #region Functions
    // Copies values from another instance
    public void CopyFrom(SerialConnectionConfig i_Source)
    {
        if (i_Source == null)
        { return; }
        SerialLineKind = i_Source.SerialLineKind;
        ComPort = i_Source.ComPort;
        BaudRate = i_Source.BaudRate;
    }

    // Creates a deep clone of this instance
    public SerialConnectionConfig DeepClone()
    {
        var clone = new SerialConnectionConfig();
        clone.CopyFrom(this);
        return clone;
    }
    #endregion
}