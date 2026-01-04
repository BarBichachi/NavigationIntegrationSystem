using System;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;
using NavigationIntegrationSystem.Infrastructure.Logging;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices;

// Represents a single device card with status, config and actions for the Devices page
public sealed partial class DeviceCardViewModel : ObservableObject
{
    #region Private Fields
    private readonly LogService m_LogService;
    private DeviceStatus m_Status;
    private bool m_IsConnected;
    private readonly Action<DeviceCardViewModel> m_OpenSettings;
    private readonly Action<DeviceCardViewModel> m_OpenInspect;
    private bool m_HasUnsavedSettings;
    #endregion

    #region Properties
    public string DeviceId { get; }
    public string DisplayName { get; }
    public DeviceType Type { get; }
    public DeviceConnectionConfig Connection => Config.Connection;
    public DeviceConfig Config { get; }
    public ObservableCollection<InspectFieldViewModel> InspectFields { get; }
    public DeviceStatus Status { get => m_Status; set => SetProperty(ref m_Status, value); }
    public bool IsConnected { get => m_IsConnected; set => SetProperty(ref m_IsConnected, value); }
    public bool AutoReconnect
    {
        get => Config.AutoReconnect;
        set
        {
            if (Config.AutoReconnect == value) { return; }
            Config.AutoReconnect = value;
            OnPropertyChanged(nameof(AutoReconnect));
        }
    }
    public bool HasUnsavedSettings { get => m_HasUnsavedSettings; set => SetProperty(ref m_HasUnsavedSettings, value); }
    #endregion

    #region Commands
    public IRelayCommand ToggleConnectCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand OpenInspectCommand { get; }
    #endregion

    #region Ctors
    public DeviceCardViewModel(string i_DeviceId, string i_DisplayName, DeviceType i_Type, DeviceConfig i_Config, LogService i_LogService,
        ObservableCollection<InspectFieldViewModel> i_InspectFields, Action<DeviceCardViewModel> i_OpenSettings, Action<DeviceCardViewModel> i_OpenInspect)
    {
        DeviceId = i_DeviceId;
        DisplayName = i_DisplayName;
        Type = i_Type;
        Config = i_Config;
        InspectFields = i_InspectFields;
        m_LogService = i_LogService;
        m_Status = DeviceStatus.Disconnected;
        m_IsConnected = false;
        m_OpenSettings = i_OpenSettings;
        m_OpenInspect = i_OpenInspect;

        ToggleConnectCommand = new RelayCommand(OnToggleConnect);
        OpenSettingsCommand = new RelayCommand(() => m_OpenSettings(this));
        OpenInspectCommand = new RelayCommand(() => m_OpenInspect(this));
    }
    #endregion

    #region Functions
    // Toggles the device connection state (stub for now)
    private void OnToggleConnect()
    {
        if (IsConnected)
        {
            IsConnected = false;
            Status = DeviceStatus.Disconnected;
            m_LogService.Info(nameof(DeviceCardViewModel), $"{DisplayName} disconnected");
            return;
        }

        IsConnected = true;
        Status = DeviceStatus.Connected;
        m_LogService.Info(nameof(DeviceCardViewModel), $"{DisplayName} connected");
    }
    #endregion
}