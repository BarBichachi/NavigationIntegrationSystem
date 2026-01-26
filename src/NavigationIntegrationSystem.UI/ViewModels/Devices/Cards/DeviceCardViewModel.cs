using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Devices.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;

// Represents a single device card with status, config and actions for the Devices page
public sealed partial class DeviceCardViewModel : ObservableObject
{
    #region Private Fields
    private readonly ILogService m_LogService;
    private readonly IInsDevice m_Device;
    private readonly Action<DeviceCardViewModel> m_OpenSettings;
    private readonly Action<DeviceCardViewModel> m_OpenInspect;
    private bool m_HasUnsavedSettings;
    #endregion

    #region Properties
    public string DisplayName => m_Device.Definition.DisplayName;
    public DeviceType Type => m_Device.Definition.Type;
    public DeviceConnectionSettings Connection => Config.Connection;
    public DeviceConfig Config { get; }
    public ObservableCollection<InspectFieldViewModel> InspectFields { get; }
    public DeviceStatus Status => m_Device.Status;
    public string? LastError => m_Device.LastError;
    public string ConnectButtonText => Status == DeviceStatus.Connected ? "Disconnect" : (Status == DeviceStatus.Connecting ? "Connecting..." : "Connect");
    public bool CanToggleConnect => Status != DeviceStatus.Connecting;
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
    public ILogService LogService => m_LogService;
    public bool IsManual => Type == DeviceType.Manual;
    public bool ShowSettingsInspect => !IsManual;
    #endregion

    #region Commands
    public IAsyncRelayCommand ToggleConnectCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand OpenInspectCommand { get; }
    #endregion

    #region Constructors
    public DeviceCardViewModel(DeviceConfig i_Config, ILogService i_LogService, ObservableCollection<InspectFieldViewModel> i_InspectFields, Action<DeviceCardViewModel> i_OpenSettings, Action<DeviceCardViewModel> i_OpenInspect, IInsDevice i_Device)
    {
        Config = i_Config;
        InspectFields = i_InspectFields;
        m_LogService = i_LogService;
        m_OpenSettings = i_OpenSettings;
        m_OpenInspect = i_OpenInspect;
        m_Device = i_Device;
        m_Device.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(LastError));
            OnPropertyChanged(nameof(ConnectButtonText));
            OnPropertyChanged(nameof(CanToggleConnect));
        };

        ToggleConnectCommand = new AsyncRelayCommand(OnToggleConnectAsync);
        OpenSettingsCommand = new RelayCommand(() => m_OpenSettings(this));
        OpenInspectCommand = new RelayCommand(() => m_OpenInspect(this));
    }
    #endregion

    #region Functions
    // Toggles the device connection state via the runtime device
    private async Task OnToggleConnectAsync()
    {
        if (Status == DeviceStatus.Connected)
        {
            m_LogService.Info(nameof(DeviceCardViewModel), $"{DisplayName} disconnect requested by user");
            await m_Device.DisconnectAsync();
            return;
        }

        if (Status == DeviceStatus.Connecting)
        {
            m_LogService.Info(nameof(DeviceCardViewModel), $"{DisplayName} connect canceled by user");
            await m_Device.DisconnectAsync();
            return;
        }

        m_LogService.Info(nameof(DeviceCardViewModel), $"{DisplayName} connect requested by user");
        await m_Device.ConnectAsync();
    }
    #endregion
}