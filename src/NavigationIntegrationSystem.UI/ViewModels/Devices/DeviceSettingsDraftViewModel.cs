using CommunityToolkit.Mvvm.ComponentModel;

using NavigationIntegrationSystem.Devices.Config;
using NavigationIntegrationSystem.Devices.Config.Enums;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices;

// Draft/form state for the settings pane (UI-only)
public sealed partial class DeviceSettingsDraftViewModel : ObservableObject
{
    #region General
    [ObservableProperty] private bool m_AutoReconnect = true;
    [ObservableProperty] private DeviceConnectionKind m_ConnectionKind = DeviceConnectionKind.Udp;
    #endregion

    #region UDP
    [ObservableProperty] private string m_UdpRemoteIp = "127.0.0.1";
    [ObservableProperty] private int m_UdpRemotePort = 5000;
    [ObservableProperty] private string m_UdpLocalIp = "0.0.0.0";
    [ObservableProperty] private int m_UdpLocalPort = 5000;
    #endregion

    #region TCP
    [ObservableProperty] private string m_TcpHost = "127.0.0.1";
    [ObservableProperty] private int m_TcpPort = 5000;
    #endregion

    #region Serial
    [ObservableProperty] private SerialLineKind m_SerialLineKind = SerialLineKind.Rs232;
    [ObservableProperty] private string m_SerialComPort = "COM1";
    [ObservableProperty] private int m_SerialBaudRate = 115200;
    #endregion

    #region Derived UI State
    public bool IsUdpSelected { get => ConnectionKind == DeviceConnectionKind.Udp; }
    public bool IsTcpSelected { get => ConnectionKind == DeviceConnectionKind.Tcp; }
    public bool IsSerialSelected { get => ConnectionKind == DeviceConnectionKind.Serial; }
    #endregion

    #region Hooks
    partial void OnConnectionKindChanged(DeviceConnectionKind value)
    {
        OnPropertyChanged(nameof(IsUdpSelected));
        OnPropertyChanged(nameof(IsTcpSelected));
        OnPropertyChanged(nameof(IsSerialSelected));
    }
    #endregion

    #region Functions
    // Loads draft values from a persisted device config
    public void LoadFrom(DeviceConfig i_Config)
    {
        if (i_Config == null) { return; }

        AutoReconnect = i_Config.AutoReconnect;
        ConnectionKind = i_Config.Connection.Kind;

        UdpRemoteIp = i_Config.Connection.Udp.RemoteIp;
        UdpRemotePort = i_Config.Connection.Udp.RemotePort;
        UdpLocalIp = i_Config.Connection.Udp.LocalIp;
        UdpLocalPort = i_Config.Connection.Udp.LocalPort;

        TcpHost = i_Config.Connection.Tcp.Host;
        TcpPort = i_Config.Connection.Tcp.Port;

        SerialLineKind = i_Config.Connection.Serial.SerialLineKind;
        SerialComPort = i_Config.Connection.Serial.ComPort;
        SerialBaudRate = i_Config.Connection.Serial.BaudRate;
    }

    // Applies draft values into an existing persisted device config (deep, safe)
    public void ApplyTo(DeviceConfig io_Config)
    {
        if (io_Config == null) { return; }

        io_Config.AutoReconnect = AutoReconnect;

        if (io_Config.Connection == null) { io_Config.Connection = new DeviceConnectionConfig(); }

        io_Config.Connection.Kind = ConnectionKind;

        io_Config.Connection.Udp.RemoteIp = UdpRemoteIp;
        io_Config.Connection.Udp.RemotePort = UdpRemotePort;
        io_Config.Connection.Udp.LocalIp = UdpLocalIp;
        io_Config.Connection.Udp.LocalPort = UdpLocalPort;

        io_Config.Connection.Tcp.Host = TcpHost;
        io_Config.Connection.Tcp.Port = TcpPort;

        io_Config.Connection.Serial.SerialLineKind = SerialLineKind;
        io_Config.Connection.Serial.ComPort = SerialComPort;
        io_Config.Connection.Serial.BaudRate = SerialBaudRate;
    }
    #endregion
}