using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using NavigationIntegrationSystem.Devices.Config;
using NavigationIntegrationSystem.Devices.Config.Enums;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices;

// Draft/form state for the settings pane (UI-only)
public sealed class DeviceSettingsDraftViewModel : INotifyPropertyChanged
{
    #region Events
    public event PropertyChangedEventHandler? PropertyChanged;
    #endregion

    #region General (Private Fields)
    private bool m_AutoReconnect = true;
    private DeviceConnectionKind m_ConnectionKind = DeviceConnectionKind.Udp;
    #endregion

    #region UDP (Private Fields)
    private string m_UdpRemoteIp = "127.0.0.1";
    private int m_UdpRemotePort = 5000;
    private string m_UdpLocalIp = "0.0.0.0";
    private int m_UdpLocalPort = 5000;
    #endregion

    #region TCP (Private Fields)
    private string m_TcpHost = "127.0.0.1";
    private int m_TcpPort = 5000;
    #endregion

    #region Serial (Private Fields)
    private SerialLineKind m_SerialLineKind = SerialLineKind.Rs232;
    private string m_SerialComPort = "COM1";
    private int m_SerialBaudRate = 115200;
    #endregion

    #region Properties
    public bool AutoReconnect { get => m_AutoReconnect; set { if (SetProperty(ref m_AutoReconnect, value)) { } } }

    public DeviceConnectionKind ConnectionKind
    {
        get => m_ConnectionKind;
        set
        {
            if (SetProperty(ref m_ConnectionKind, value))
            {
                OnPropertyChanged(nameof(IsUdpSelected));
                OnPropertyChanged(nameof(IsTcpSelected));
                OnPropertyChanged(nameof(IsSerialSelected));
            }
        }
    }

    public string UdpRemoteIp { get => m_UdpRemoteIp; set => SetProperty(ref m_UdpRemoteIp, value); }
    public int UdpRemotePort { get => m_UdpRemotePort; set => SetProperty(ref m_UdpRemotePort, value); }
    public string UdpLocalIp { get => m_UdpLocalIp; set => SetProperty(ref m_UdpLocalIp, value); }
    public int UdpLocalPort { get => m_UdpLocalPort; set => SetProperty(ref m_UdpLocalPort, value); }

    public string TcpHost { get => m_TcpHost; set => SetProperty(ref m_TcpHost, value); }
    public int TcpPort { get => m_TcpPort; set => SetProperty(ref m_TcpPort, value); }

    public SerialLineKind SerialLineKind { get => m_SerialLineKind; set => SetProperty(ref m_SerialLineKind, value); }
    public string SerialComPort { get => m_SerialComPort; set => SetProperty(ref m_SerialComPort, value); }
    public int SerialBaudRate { get => m_SerialBaudRate; set => SetProperty(ref m_SerialBaudRate, value); }
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

    #region Helpers
    // Raises PropertyChanged for a property name
    private void OnPropertyChanged([CallerMemberName] string? i_PropertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(i_PropertyName));
    }

    // Sets a backing field and raises PropertyChanged when the value changes
    private bool SetProperty<T>(ref T io_Field, T i_Value, [CallerMemberName] string? i_PropertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(io_Field, i_Value)) { return false; }
        io_Field = i_Value;
        OnPropertyChanged(i_PropertyName);
        return true;
    }
    #endregion
}
