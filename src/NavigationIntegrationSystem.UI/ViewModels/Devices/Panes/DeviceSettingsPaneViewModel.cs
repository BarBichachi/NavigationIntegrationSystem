using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using NavigationIntegrationSystem.Core.Playback;
using NavigationIntegrationSystem.Devices.Enums;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.Devices.Validation;
using NavigationIntegrationSystem.UI.Enums;
using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.Services.UI.FilePicking;
using NavigationIntegrationSystem.UI.ViewModels.Base;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

public sealed partial class DeviceSettingsPaneViewModel : ViewModelBase
{
    #region Private Fields
    private readonly DevicesViewModel m_Parent;
    private readonly DeviceCardViewModel m_Device;
    private readonly IFilePickerService m_FilePickerService;
    private readonly IPlaybackService m_PlaybackService;
    private readonly IDialogService m_DialogService;
    private DeviceConfig m_OriginalSnapshot;
    private bool m_HasUnsavedChanges;
    private bool m_IsLoadingDraft;
    private XamlRoot? m_XamlRoot;
    #endregion

    #region Properties
    public DeviceCardViewModel Device => m_Device;
    public DeviceSettingsDraftViewModel Draft { get; }
    public ObservableCollection<DeviceConnectionKind> ConnectionKinds { get; } = new ObservableCollection<DeviceConnectionKind>((DeviceConnectionKind[])Enum.GetValues(typeof(DeviceConnectionKind)));
    public ObservableCollection<SerialLineKind> SerialLineKinds { get; } = new ObservableCollection<SerialLineKind>((SerialLineKind[])Enum.GetValues(typeof(SerialLineKind)));
    public ObservableCollection<int> Frequencies { get; } = new ObservableCollection<int> { 10, 25, 50, 100 };
    public bool HasUnsavedChanges { get => m_HasUnsavedChanges; set { if (SetProperty(ref m_HasUnsavedChanges, value)) { m_Device.HasUnsavedSettings = value; } } }
    public bool IsPlaybackDevice => m_Device.Type == Core.Enums.DeviceType.Playback;
    #endregion

    #region Commands
    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand DiscardCommand { get; }
    public IAsyncRelayCommand BrowseFileCommand { get; }
    public IAsyncRelayCommand ExportTemplateCommand { get; }
    #endregion

    #region Ctors
    public DeviceSettingsPaneViewModel(DevicesViewModel i_Parent, DeviceCardViewModel i_Device,
        IFilePickerService i_FilePickerService, IPlaybackService i_PlaybackService, IDialogService i_DialogService)
    {
        m_Parent = i_Parent;
        m_Device = i_Device;
        m_FilePickerService = i_FilePickerService;
        m_PlaybackService = i_PlaybackService;
        m_DialogService = i_DialogService;

        Draft = new DeviceSettingsDraftViewModel();

        ApplyCommand = new RelayCommand(() => _ = TryApplyAsync());
        DiscardCommand = new RelayCommand(Discard);
        BrowseFileCommand = new AsyncRelayCommand(OnBrowseFileAsync);
        ExportTemplateCommand = new AsyncRelayCommand(OnExportTemplateAsync);

        // 1. Take initial snapshot
        m_OriginalSnapshot = m_Device.Config.DeepClone();

        // 2. Load it into Draft
        LoadDraftFromSnapshot();

        // 3. Listen for changes
        Draft.PropertyChanged += OnDraftPropertyChanged;
    }
    #endregion

    #region Functions
    // Checks for unsaved changes and prompts the user accordingly
    public async Task<bool> CanCloseAsync()
    {
        if (!HasUnsavedChanges) return true;
        if (m_XamlRoot == null) return true;

        // Show Dialog
        DialogCloseDecision decision = await m_DialogService.ShowUnsavedChangesDialogAsync(m_XamlRoot);

        switch (decision)
        {
            case DialogCloseDecision.Apply:
                return await TryApplyAsync();

            case DialogCloseDecision.Discard:
                Discard();
                return true;

            case DialogCloseDecision.Cancel:
            default:
                return false;
        }
    }

    public void SetXamlRoot(XamlRoot i_XamlRoot) { m_XamlRoot = i_XamlRoot; }

    public async Task<bool> TryApplyAsync()
    {
        // 1. Create a temp config to validate against
        var tempConfig = m_OriginalSnapshot.DeepClone();
        Draft.ApplyTo(tempConfig);

        // 2. Validate
        var errors = ConnectionSettingsValidator.Validate(tempConfig.Connection, m_Device.Type);
        if (errors.Count > 0)
        {
            await m_DialogService.ShowValidationFailedDialogAsync(m_XamlRoot!, string.Join("\n", errors));
            return false;
        }

        // 3. Save to Real Device Config
        Draft.ApplyTo(m_Device.Config);
        await m_Parent.SaveDevicesConfigCommand.ExecuteAsync(null);

        // 4. Update Snapshot (Make this the new "Clean" state)
        m_OriginalSnapshot = m_Device.Config.DeepClone();
        UpdateDirtyState();

        m_Parent.ForceClosePaneAfterApply();
        return true;
    }

    // Reverts Draft to match the last saved snapshot, then updates dirty state
    public void Discard()
    {
        m_Device.Config.CopyFrom(m_OriginalSnapshot);
        LoadDraftFromSnapshot();
        UpdateDirtyState();
    }

    // Loads the Draft from the snapshot without triggering dirty state updates
    private void LoadDraftFromSnapshot()
    {
        m_IsLoadingDraft = true;
        Draft.LoadFrom(m_OriginalSnapshot);
        m_IsLoadingDraft = false;
    }

    // Whenever any Draft property changes, check if we are now dirty compared to the snapshot.
    private void OnDraftPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (m_IsLoadingDraft) return;
        UpdateDirtyState();
    }

    // Updates the dirty state based on the current Draft and the original snapshot.
    private void UpdateDirtyState()
    {
        HasUnsavedChanges = !IsDraftEqualToSnapshot(Draft, m_OriginalSnapshot) || HasInvalidNumericText();
    }

    // Compares the Draft with the snapshot to determine if there are unsaved changes (ignoring invalid text states)
    private static bool IsDraftEqualToSnapshot(DeviceSettingsDraftViewModel draft, DeviceConfig snapshot)
    {
        if (draft.AutoReconnect != snapshot.AutoReconnect) return false;
        if (draft.ConnectionKind != snapshot.Connection.Kind) return false;

        var c = snapshot.Connection;

        // UDP
        if (draft.UdpRemoteIp != c.Udp.RemoteIp) return false;
        if (draft.UdpRemotePort != c.Udp.RemotePort) return false;
        if (draft.UdpLocalIp != c.Udp.LocalIp) return false;
        if (draft.UdpLocalPort != c.Udp.LocalPort) return false;

        // TCP
        if (draft.TcpHost != c.Tcp.Host) return false;
        if (draft.TcpPort != c.Tcp.Port) return false;

        // Serial
        if (draft.SerialLineKind != c.Serial.SerialLineKind) return false;
        if (draft.SerialComPort != c.Serial.ComPort) return false;
        if (draft.SerialBaudRate != c.Serial.BaudRate) return false;

        // Playback
        if (draft.PlaybackFilePath != c.Playback.FilePath) return false;
        if (draft.PlaybackLoop != c.Playback.Loop) return false;
        if (draft.PlaybackFrequency != c.Playback.Frequency) return false;

        return true;
    }

    // Checks if the text boxes contain invalid or different values than the int backing fields
    private bool HasInvalidNumericText()
    {
        if (!IsTextIntMatch(Draft.UdpRemotePortText, m_OriginalSnapshot.Connection.Udp.RemotePort)) return true;
        if (!IsTextIntMatch(Draft.UdpLocalPortText, m_OriginalSnapshot.Connection.Udp.LocalPort)) return true;
        if (!IsTextIntMatch(Draft.TcpPortText, m_OriginalSnapshot.Connection.Tcp.Port)) return true;
        if (!IsTextIntMatch(Draft.SerialBaudRateText, m_OriginalSnapshot.Connection.Serial.BaudRate)) return true;
        return false;
    }

    // Helper to check if a text input matches the int value (valid and equal)
    private static bool IsTextIntMatch(string text, int value)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!int.TryParse(text, out int parsed)) return false;

        return parsed == value;
    }
    #endregion

    #region Handlers
    // Opens a file picker for CSV files and updates the Draft's PlaybackFilePath if a file is selected
    private async Task OnBrowseFileAsync()
    {
        string? path = await m_FilePickerService.PickSingleFileAsync(new[] { ".csv" });
        if (!string.IsNullOrEmpty(path)) Draft.PlaybackFilePath = path;
    }

    // Opens a file picker to select where to save a CSV template, then calls the PlaybackService to create it
    private async Task OnExportTemplateAsync()
    {
        string? path = await m_FilePickerService.PickSaveFileAsync("Playback_Template.csv", new Dictionary<string, IList<string>> { { "CSV", new[] { ".csv" } } });
        if (!string.IsNullOrEmpty(path)) await m_PlaybackService.CreateTemplateAsync(path);
    }
    #endregion
}