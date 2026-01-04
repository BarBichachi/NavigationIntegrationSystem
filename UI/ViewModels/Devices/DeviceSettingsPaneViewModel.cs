using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices;

// ViewModel for the settings pane of a selected device (draft-based)
public sealed partial class DeviceSettingsPaneViewModel : ObservableObject
{
    #region Private Fields
    private readonly DevicesViewModel m_Parent;
    private readonly DeviceCardViewModel m_Device;
    private DeviceConfig m_DraftConfig;
    private bool m_HasUnsavedChanges;
    private PropertyChangedEventHandler? m_DraftHandler;
    private PropertyChangedEventHandler? m_ConnectionHandler;
    private PropertyChangedEventHandler? m_UdpHandler;
    private PropertyChangedEventHandler? m_TcpHandler;
    private PropertyChangedEventHandler? m_SerialHandler;
    #endregion

    #region Properties
    public DeviceCardViewModel Device => m_Device;
    public DeviceConfig DraftConfig { get => m_DraftConfig; private set => SetProperty(ref m_DraftConfig, value); }
    public ObservableCollection<DeviceConnectionKind> ConnectionKinds { get; } = new ObservableCollection<DeviceConnectionKind>((DeviceConnectionKind[])Enum.GetValues(typeof(DeviceConnectionKind)));
    public ObservableCollection<SerialLineKind> SerialLineKinds { get; } = new ObservableCollection<SerialLineKind>((SerialLineKind[])Enum.GetValues(typeof(SerialLineKind)));
    public bool HasUnsavedChanges { get => m_HasUnsavedChanges; set => SetProperty(ref m_HasUnsavedChanges, value); }
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

        HookDraftChangeTracking();
        HasUnsavedChanges = false;
    }
    #endregion

    #region Functions
    // Hooks draft change tracking safely
    private void HookDraftChangeTracking()
    {
        UnhookDraftChangeTracking();

        m_DraftHandler = (_, __) => HasUnsavedChanges = true;
        m_ConnectionHandler = (_, __) => HasUnsavedChanges = true;
        m_UdpHandler = (_, __) => HasUnsavedChanges = true;
        m_TcpHandler = (_, __) => HasUnsavedChanges = true;
        m_SerialHandler = (_, __) => HasUnsavedChanges = true;

        DraftConfig.PropertyChanged += m_DraftHandler;
        DraftConfig.Connection.PropertyChanged += m_ConnectionHandler;
        DraftConfig.Connection.Udp.PropertyChanged += m_UdpHandler;
        DraftConfig.Connection.Tcp.PropertyChanged += m_TcpHandler;
        DraftConfig.Connection.Serial.PropertyChanged += m_SerialHandler;
    }

    // Unhooks draft change tracking handlers
    private void UnhookDraftChangeTracking()
    {
        if (m_DraftHandler != null) { DraftConfig.PropertyChanged -= m_DraftHandler; }
        if (m_ConnectionHandler != null) { DraftConfig.Connection.PropertyChanged -= m_ConnectionHandler; }
        if (m_UdpHandler != null) { DraftConfig.Connection.Udp.PropertyChanged -= m_UdpHandler; }
        if (m_TcpHandler != null) { DraftConfig.Connection.Tcp.PropertyChanged -= m_TcpHandler; }
        if (m_SerialHandler != null) { DraftConfig.Connection.Serial.PropertyChanged -= m_SerialHandler; }

        m_DraftHandler = null;
        m_ConnectionHandler = null;
        m_UdpHandler = null;
        m_TcpHandler = null;
        m_SerialHandler = null;
    }

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
        m_DraftConfig = m_Device.Config.DeepClone();
        OnPropertyChanged(nameof(DraftConfig));
        HookDraftChangeTracking();
        HasUnsavedChanges = false;
        m_Device.HasUnsavedSettings = false;
    }

    // Applies draft into real config, saves, clears warning, closes
    private void OnApply() { Apply(); }
    #endregion
}
