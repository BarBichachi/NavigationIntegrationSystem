using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;

// Represents a single device card with status, config and actions for the Devices page
public sealed partial class DeviceCardViewModel : ViewModelBase
{
    #region Private Fields
    private readonly ILogService m_LogService;
    private readonly IDialogService m_DialogService;
    private readonly IInsDevice m_Device;
    private readonly Action<DeviceCardViewModel> m_OpenSettings;
    private readonly Action<DeviceCardViewModel> m_OpenInspect;
    // Captured at construction (which runs on the UI thread). Used to marshal StateChanged property notifications back to UI when the underlying device fires the event from a background thread (e.g. VN310's watchdog timer callback after 2s of telemetry silence)
    private readonly DispatcherQueue m_DispatcherQueue;
    private bool m_HasUnsavedSettings;
    #endregion

    #region Properties
    public string DisplayName => m_Device.Definition.DisplayName;
    public IInsDevice Device => m_Device;
    public DeviceType Type => m_Device.Definition.Type;
    public DeviceConnectionSettings Connection => Config.Connection;
    public DeviceConfig Config { get; }
    public ObservableCollection<InspectFieldViewModel> InspectFields { get; }
    public DeviceStatus Status => m_Device.Status;
    public string? LastError => m_Device.LastError;
    public string ConnectButtonText => Status switch
    {
        DeviceStatus.Connected => "Disconnect",
        DeviceStatus.Connecting => "Connecting...",
        DeviceStatus.Error => "Retry",
        _ => "Connect"
    };
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
    public bool IsSettingsVisible => Type != DeviceType.Manual;
    public bool IsInspectVisible => Type != DeviceType.Manual && Type != DeviceType.Playback;
    public string? ModeLabel => m_Device.CurrentMode?.Label;
    public DeviceModeSeverity ModeSeverity => m_Device.CurrentMode?.Severity ?? DeviceModeSeverity.Unknown;
    #endregion

    #region Commands
    public IAsyncRelayCommand ToggleConnectCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand OpenInspectCommand { get; }
    #endregion

    #region Constructors
    public DeviceCardViewModel(DeviceConfig i_Config, ILogService i_LogService, IDialogService i_DialogService, ObservableCollection<InspectFieldViewModel> i_InspectFields, Action<DeviceCardViewModel> i_OpenSettings, Action<DeviceCardViewModel> i_OpenInspect, IInsDevice i_Device)
    {
        Config = i_Config;
        InspectFields = i_InspectFields;
        m_LogService = i_LogService;
        m_DialogService = i_DialogService;
        m_OpenSettings = i_OpenSettings;
        m_OpenInspect = i_OpenInspect;
        m_Device = i_Device;
        m_DispatcherQueue = DispatcherQueue.GetForCurrentThread();
        m_Device.StateChanged += OnDeviceStateChanged;
        m_Device.ModeChanged += OnDeviceModeChanged;

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

        // Check if connection failed and show dialog if so
        if (Status == DeviceStatus.Error)
        {
            string msg = !string.IsNullOrWhiteSpace(LastError) ? LastError : "Unknown error occurred during connection.";
            await m_DialogService.ShowErrorAsync($"Connection Failed ({DisplayName})", msg);
        }
    }

    // Raises the state-derived property notifications. Pulled into a method so OnDeviceStateChanged can route through DispatcherQueue when the underlying StateChanged was fired off the UI thread (e.g. VN310 watchdog timer)
    private void RaiseStatePropertyChanges()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(LastError));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(CanToggleConnect));
    }

    private void RaiseModePropertyChanges()
    {
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(ModeSeverity));
    }
    #endregion

    #region Event Handlers
    // Marshals state-change notifications back to the UI thread. The Vn310 watchdog raises Stalled (and thus StateChanged) from a System.Threading.Timer callback; without this hop, x:Bind doesn't pick up the property changes until the next layout pass (visible to the user as "the card doesn't update until I switch pages and come back")
    private void OnDeviceStateChanged(object? i_Sender, EventArgs i_Args)
    {
        if (m_DispatcherQueue.HasThreadAccess)
        {
            RaiseStatePropertyChanges();
        }
        else
        {
            m_DispatcherQueue.TryEnqueue(RaiseStatePropertyChanges);
        }
    }

    private void OnDeviceModeChanged(object? i_Sender, EventArgs i_Args)
    {
        if (m_DispatcherQueue.HasThreadAccess)
        {
            RaiseModePropertyChanges();
        }
        else
        {
            m_DispatcherQueue.TryEnqueue(RaiseModePropertyChanges);
        }
    }
    #endregion
}