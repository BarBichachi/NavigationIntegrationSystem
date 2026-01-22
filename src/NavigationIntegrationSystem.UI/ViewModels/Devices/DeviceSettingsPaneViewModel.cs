using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NavigationIntegrationSystem.Devices.Config;
using NavigationIntegrationSystem.Devices.Config.Enums;
using System;
using System.Collections.ObjectModel;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices;

// ViewModel for the settings pane of a selected device (draft-based)
public sealed partial class DeviceSettingsPaneViewModel : ObservableObject
{
    #region Private Fields
    private readonly DevicesViewModel m_Parent;
    private readonly DeviceCardViewModel m_Device;
    private DeviceConfig m_DraftConfig;
    private bool m_HasUnsavedChanges;
    #endregion

    #region Properties
    public DeviceCardViewModel Device => m_Device;
    public DeviceConfig DraftConfig { get => m_DraftConfig; private set => SetProperty(ref m_DraftConfig, value); }
    public ObservableCollection<DeviceConnectionKind> ConnectionKinds { get; } = new ObservableCollection<DeviceConnectionKind>((DeviceConnectionKind[])Enum.GetValues(typeof(DeviceConnectionKind)));
    public ObservableCollection<SerialLineKind> SerialLineKinds { get; } = new ObservableCollection<SerialLineKind>((SerialLineKind[])Enum.GetValues(typeof(SerialLineKind)));
    public bool HasUnsavedChanges { get => m_HasUnsavedChanges; set => SetProperty(ref m_HasUnsavedChanges, value); }
    public bool AutoReconnect
    {
        get => DraftConfig.AutoReconnect;
        set
        {
            if (DraftConfig.AutoReconnect == value) { return; }
            DraftConfig.AutoReconnect = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public DeviceConnectionKind ConnectionKind
    {
        get => DraftConfig.Connection.Kind;
        set
        {
            if (DraftConfig.Connection.Kind == value) { return; }
            DraftConfig.Connection.Kind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsUdpSelected));
            OnPropertyChanged(nameof(IsTcpSelected));
            OnPropertyChanged(nameof(IsSerialSelected));
            MarkDirty();
        }
    }

    public bool IsUdpSelected { get => ConnectionKind == DeviceConnectionKind.Udp; }
    public bool IsTcpSelected { get => ConnectionKind == DeviceConnectionKind.Tcp; }
    public bool IsSerialSelected { get => ConnectionKind == DeviceConnectionKind.Serial; }

    public string UdpRemoteIp
    {
        get => DraftConfig.Connection.Udp.RemoteIp;
        set
        {
            if (DraftConfig.Connection.Udp.RemoteIp == value) { return; }
            DraftConfig.Connection.Udp.RemoteIp = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public int UdpRemotePort
    {
        get => DraftConfig.Connection.Udp.RemotePort;
        set
        {
            if (DraftConfig.Connection.Udp.RemotePort == value) { return; }
            DraftConfig.Connection.Udp.RemotePort = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public string UdpLocalIp
    {
        get => DraftConfig.Connection.Udp.LocalIp;
        set
        {
            if (DraftConfig.Connection.Udp.LocalIp == value) { return; }
            DraftConfig.Connection.Udp.LocalIp = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public int UdpLocalPort
    {
        get => DraftConfig.Connection.Udp.LocalPort;
        set
        {
            if (DraftConfig.Connection.Udp.LocalPort == value) { return; }
            DraftConfig.Connection.Udp.LocalPort = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }
    public string TcpHost
    {
        get => DraftConfig.Connection.Tcp.Host;
        set
        {
            if (DraftConfig.Connection.Tcp.Host == value) { return; }
            DraftConfig.Connection.Tcp.Host = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public int TcpPort
    {
        get => DraftConfig.Connection.Tcp.Port;
        set
        {
            if (DraftConfig.Connection.Tcp.Port == value) { return; }
            DraftConfig.Connection.Tcp.Port = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }
    public SerialLineKind SerialLineKind
    {
        get => DraftConfig.Connection.Serial.SerialLineKind;
        set
        {
            if (DraftConfig.Connection.Serial.SerialLineKind == value) { return; }
            DraftConfig.Connection.Serial.SerialLineKind = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public string SerialComPort
    {
        get => DraftConfig.Connection.Serial.ComPort;
        set
        {
            if (DraftConfig.Connection.Serial.ComPort == value) { return; }
            DraftConfig.Connection.Serial.ComPort = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public int SerialBaudRate
    {
        get => DraftConfig.Connection.Serial.BaudRate;
        set
        {
            if (DraftConfig.Connection.Serial.BaudRate == value) { return; }
            DraftConfig.Connection.Serial.BaudRate = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    #endregion

    #region Commands
    public IRelayCommand ApplyCommand { get; }
    #endregion

    #region Ctors
    public DeviceSettingsPaneViewModel(DevicesViewModel i_Parent, DeviceCardViewModel i_Device)
    {
        m_Parent = i_Parent;
        m_Device = i_Device;

        m_DraftConfig = i_Device.Config.DeepClone();
        ApplyCommand = new RelayCommand(OnApply);

        HasUnsavedChanges = false;
    }
    #endregion

    #region Functions
    // Applies draft into real config, saves, clears warning, closes
    public void Apply()
    {
        m_Device.Config.CopyFrom(DraftConfig);
        m_Parent.SaveDevicesConfigCommand.Execute(null);
        HasUnsavedChanges = false;
        m_Device.HasUnsavedSettings = false;
        m_Parent.ForceClosePaneAfterApply();
    }

    // Discards draft changes and restores draft from device config
    public void Discard()
    {
        DraftConfig = m_Device.Config.DeepClone();

        OnPropertyChanged(nameof(AutoReconnect));
        OnPropertyChanged(nameof(ConnectionKind));
        OnPropertyChanged(nameof(IsUdpSelected));
        OnPropertyChanged(nameof(IsTcpSelected));
        OnPropertyChanged(nameof(IsSerialSelected));

        OnPropertyChanged(nameof(UdpRemoteIp));
        OnPropertyChanged(nameof(UdpRemotePort));
        OnPropertyChanged(nameof(UdpLocalIp));
        OnPropertyChanged(nameof(UdpLocalPort));

        OnPropertyChanged(nameof(TcpHost));
        OnPropertyChanged(nameof(TcpPort));

        OnPropertyChanged(nameof(SerialLineKind));
        OnPropertyChanged(nameof(SerialComPort));
        OnPropertyChanged(nameof(SerialBaudRate));

        HasUnsavedChanges = false;
        m_Device.HasUnsavedSettings = false;
    }


    // Marks the current draft as modified
    private void MarkDirty()
    {
        HasUnsavedChanges = true;
        m_Device.HasUnsavedSettings = true;
    }

    // Applies draft into real config, saves, clears warning, closes
    private void OnApply() 
    {
        Apply(); 
    }
    #endregion
}
