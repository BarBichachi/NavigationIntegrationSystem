using NavigationIntegrationSystem.Devices.Config;
using NavigationIntegrationSystem.Devices.Config.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Base;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices;

// Draft/form state for the settings pane (UI-only)
public sealed class DeviceSettingsDraftViewModel : ViewModelBase
{
    #region General (Private Fields)
    private bool m_AutoReconnect = true;
    private DeviceConnectionKind m_ConnectionKind = DeviceConnectionKind.Udp;
    #endregion

    #region UDP (Private Fields)
    private string m_UdpRemoteIp = "127.0.0.1";
    private string m_UdpRemotePortText = "5000";
    private int m_UdpRemotePort = 5000;
    private string m_UdpLocalIp = "0.0.0.0";
    private string m_UdpLocalPortText = "5000";
    private int m_UdpLocalPort = 5000;
    #endregion

    #region TCP (Private Fields)
    private string m_TcpHost = "127.0.0.1";
    private string m_TcpPortText = "5000";
    private int m_TcpPort = 5000;
    #endregion

    #region Serial (Private Fields)
    private SerialLineKind m_SerialLineKind = SerialLineKind.Rs232;
    private string m_SerialComPort = "COM1";
    private string m_SerialBaudRateText = "115200";
    private int m_SerialBaudRate = 115200;
    #endregion

    #region Properties
    public bool AutoReconnect { get => m_AutoReconnect; set => SetProperty(ref m_AutoReconnect, value); }

    public DeviceConnectionKind ConnectionKind
    {
        get => m_ConnectionKind;
        set
        {
            if (!SetProperty(ref m_ConnectionKind, value)) { return; }
            OnPropertyChanged(nameof(IsUdpSelected));
            OnPropertyChanged(nameof(IsTcpSelected));
            OnPropertyChanged(nameof(IsSerialSelected));
        }
    }

    public string UdpRemoteIp { get => m_UdpRemoteIp; set => SetProperty(ref m_UdpRemoteIp, value); }
    public int UdpRemotePort { get => m_UdpRemotePort; set => SetProperty(ref m_UdpRemotePort, value); }
    public string UdpRemotePortText { get => m_UdpRemotePortText; set { if (SetProperty(ref m_UdpRemotePortText, value)) { if (int.TryParse(value, out int port)) { UdpRemotePort = port; } } } }
    public string UdpLocalIp { get => m_UdpLocalIp; set => SetProperty(ref m_UdpLocalIp, value); }
    public int UdpLocalPort { get => m_UdpLocalPort; set => SetProperty(ref m_UdpLocalPort, value); }
    public string UdpLocalPortText { get => m_UdpLocalPortText; set { if (SetProperty(ref m_UdpLocalPortText, value)) { if (int.TryParse(value, out int port)) { UdpLocalPort = port; } } } }

    public string TcpHost { get => m_TcpHost; set => SetProperty(ref m_TcpHost, value); }
    public int TcpPort { get => m_TcpPort; set => SetProperty(ref m_TcpPort, value); }
    public string TcpPortText { get => m_TcpPortText; set { if (SetProperty(ref m_TcpPortText, value)) { if (int.TryParse(value, out int port)) { TcpPort = port; } } } }

    public SerialLineKind SerialLineKind { get => m_SerialLineKind; set => SetProperty(ref m_SerialLineKind, value); }
    public string SerialComPort { get => m_SerialComPort; set => SetProperty(ref m_SerialComPort, value); }
    public int SerialBaudRate { get => m_SerialBaudRate; set => SetProperty(ref m_SerialBaudRate, value); }
    public string SerialBaudRateText { get => m_SerialBaudRateText; set { if (SetProperty(ref m_SerialBaudRateText, value)) { if (int.TryParse(value, out int rate)) { SerialBaudRate = rate; } } } }
    #endregion

    #region Derived UI State
    public bool IsUdpSelected { get => ConnectionKind == DeviceConnectionKind.Udp; }
    public bool IsTcpSelected { get => ConnectionKind == DeviceConnectionKind.Tcp; }
    public bool IsSerialSelected { get => ConnectionKind == DeviceConnectionKind.Serial; }
    #endregion

    #region Functions
    // Loads draft values from a persisted device config
    public void LoadFrom(DeviceConfig i_Config)
    {
        if (i_Config == null) { return; }

        AutoReconnect = i_Config.AutoReconnect;

        if (i_Config.Connection == null) { i_Config.Connection = new DeviceConnectionSettings(); }
        LoadFrom(i_Config.Connection);
    }

    // Loads draft values from persisted connection settings
    public void LoadFrom(DeviceConnectionSettings i_Settings)
    {
        if (i_Settings == null) { return; }

        ConnectionKind = i_Settings.Kind;

        UdpRemoteIp = i_Settings.Udp.RemoteIp;
        UdpRemotePort = i_Settings.Udp.RemotePort;
        UdpRemotePortText = UdpRemotePort.ToString();
        UdpLocalIp = i_Settings.Udp.LocalIp;
        UdpLocalPort = i_Settings.Udp.LocalPort;
        UdpLocalPortText = UdpLocalPort.ToString();

        TcpHost = i_Settings.Tcp.Host;
        TcpPort = i_Settings.Tcp.Port;
        TcpPortText = TcpPort.ToString();

        SerialLineKind = i_Settings.Serial.SerialLineKind;
        SerialComPort = i_Settings.Serial.ComPort;
        SerialBaudRate = i_Settings.Serial.BaudRate;
        SerialBaudRateText = SerialBaudRate.ToString();
    }

    // Applies draft values into an existing persisted device config (deep, safe)
    public void ApplyTo(DeviceConfig io_Config)
    {
        if (io_Config == null) { return; }

        io_Config.AutoReconnect = AutoReconnect;

        if (io_Config.Connection == null) { io_Config.Connection = new DeviceConnectionSettings(); }
        ApplyTo(io_Config.Connection);
    }

    // Applies draft values into persisted connection settings (deep, safe)
    public void ApplyTo(DeviceConnectionSettings io_Settings)
    {
        if (io_Settings == null) { return; }

        io_Settings.Kind = ConnectionKind;

        if (io_Settings.Udp == null) { io_Settings.Udp = new(); }
        if (io_Settings.Tcp == null) { io_Settings.Tcp = new(); }
        if (io_Settings.Serial == null) { io_Settings.Serial = new(); }

        io_Settings.Udp.RemoteIp = UdpRemoteIp;
        io_Settings.Udp.RemotePort = UdpRemotePort;
        io_Settings.Udp.LocalIp = UdpLocalIp;
        io_Settings.Udp.LocalPort = UdpLocalPort;

        io_Settings.Tcp.Host = TcpHost;
        io_Settings.Tcp.Port = TcpPort;

        io_Settings.Serial.SerialLineKind = SerialLineKind;
        io_Settings.Serial.ComPort = SerialComPort;
        io_Settings.Serial.BaudRate = SerialBaudRate;
    }

    #endregion
}