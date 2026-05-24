using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using NavigationIntegrationSystem.Devices.Enums;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.Devices.Validation;
using NavigationIntegrationSystem.UI.Enums;
using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.ViewModels.Base;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

// Common logic for any device settings pane: holds the Draft, takes/restores snapshots, tracks dirty state, validates, and saves. Subclasses add per-device knobs (e.g. file pickers for Playback, recommended-hint accessors for VN310). The pane host (DeviceSettingsPaneView) binds to this base type and a DataTemplate selects the matching View per concrete subclass
public abstract partial class DeviceSettingsPaneViewModelBase : ViewModelBase
{
    #region Static Lookups
    // ItemsSource references MUST be stable across VM instances. Each new VM was previously creating a fresh ObservableCollection; when the settings pane re-opened with a new VM, ComboBox saw a different collection reference, cleared its SelectedItem mid-rebuild, and the TwoWay binding's generated cast `(SerialLineKind)null` threw NullReferenceException. Sharing one immutable list per enum avoids the clear
    private static readonly IReadOnlyList<DeviceConnectionKind> s_ConnectionKinds = (DeviceConnectionKind[])Enum.GetValues(typeof(DeviceConnectionKind));
    private static readonly IReadOnlyList<SerialLineKind> s_SerialLineKinds = (SerialLineKind[])Enum.GetValues(typeof(SerialLineKind));
    #endregion

    #region Properties
    public DeviceCardViewModel Device { get; }
    public DeviceSettingsDraftViewModel Draft { get; }
    public IReadOnlyList<DeviceConnectionKind> ConnectionKinds => s_ConnectionKinds;
    public IReadOnlyList<SerialLineKind> SerialLineKinds => s_SerialLineKinds;
    public bool HasUnsavedChanges
    {
        get => m_HasUnsavedChanges;
        set
        {
            if (SetProperty(ref m_HasUnsavedChanges, value)) { Device.HasUnsavedSettings = value; }
        }
    }
    #endregion

    #region Private Fields
    private readonly DevicesViewModel m_Parent;
    private readonly IDialogService m_DialogService;
    private DeviceConfig m_OriginalSnapshot;
    private bool m_HasUnsavedChanges;
    private bool m_IsLoadingDraft;
    private XamlRoot? m_XamlRoot;
    #endregion

    #region Commands
    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand DiscardCommand { get; }
    #endregion

    #region Constructors
    protected DeviceSettingsPaneViewModelBase(DevicesViewModel i_Parent, DeviceCardViewModel i_Device, IDialogService i_DialogService)
    {
        m_Parent = i_Parent;
        Device = i_Device;
        m_DialogService = i_DialogService;

        Draft = new DeviceSettingsDraftViewModel();

        ApplyCommand = new RelayCommand(() => _ = TryApplyAsync());
        DiscardCommand = new RelayCommand(Discard);

        // 1. Take initial snapshot
        m_OriginalSnapshot = Device.Config.DeepClone();

        // 2. Load it into Draft
        LoadDraftFromSnapshot();

        // 3. Listen for changes
        Draft.PropertyChanged += OnDraftPropertyChanged;
    }
    #endregion

    #region Functions
    public void SetXamlRoot(XamlRoot i_XamlRoot) { m_XamlRoot = i_XamlRoot; }

    // Checks for unsaved changes and prompts the user accordingly
    public async Task<bool> CanCloseAsync()
    {
        if (!HasUnsavedChanges) { return true; }
        if (m_XamlRoot == null) { return true; }

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

    // Validates the draft against the connection rules for this device type, persists on success, and triggers the parent's pane-close logic
    public async Task<bool> TryApplyAsync()
    {
        // 1. Create a temp config to validate against
        DeviceConfig tempConfig = m_OriginalSnapshot.DeepClone();
        Draft.ApplyTo(tempConfig);

        // 2. Validate
        IReadOnlyList<string> errors = ConnectionSettingsValidator.Validate(tempConfig.Connection, Device.Type);
        if (errors.Count > 0)
        {
            await m_DialogService.ShowValidationFailedDialogAsync(m_XamlRoot!, string.Join("\n", errors));
            return false;
        }

        // 3. Save to Real Device Config
        Draft.ApplyTo(Device.Config);
        await m_Parent.SaveDevicesConfigCommand.ExecuteAsync(null);

        // 4. Update Snapshot (Make this the new "Clean" state)
        m_OriginalSnapshot = Device.Config.DeepClone();
        UpdateDirtyState();

        m_Parent.ForceClosePaneAfterApply();
        return true;
    }

    // Reverts Draft to match the last saved snapshot, then updates dirty state
    public void Discard()
    {
        Device.Config.CopyFrom(m_OriginalSnapshot);
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

    // Updates the dirty state based on the current Draft and the original snapshot
    private void UpdateDirtyState()
    {
        HasUnsavedChanges = !IsDraftEqualToSnapshot(Draft, m_OriginalSnapshot) || HasInvalidNumericText();
    }

    // Compares the Draft with the snapshot to determine if there are unsaved changes (ignoring invalid text states). The Draft holds the union of all section fields (UDP/TCP/Serial/Playback) because DeviceConfig.Connection is unified -- subsections that this device's pane doesn't expose are still compared, but they stay equal to the snapshot because nothing edits them
    private static bool IsDraftEqualToSnapshot(DeviceSettingsDraftViewModel i_Draft, DeviceConfig i_Snapshot)
    {
        if (i_Draft.AutoReconnect != i_Snapshot.AutoReconnect) { return false; }
        if (i_Draft.ConnectionKind != i_Snapshot.Connection.Kind) { return false; }

        DeviceConnectionSettings c = i_Snapshot.Connection;

        // UDP
        if (i_Draft.UdpRemoteIp != c.Udp.RemoteIp) { return false; }
        if (i_Draft.UdpRemotePort != c.Udp.RemotePort) { return false; }
        if (i_Draft.UdpLocalIp != c.Udp.LocalIp) { return false; }
        if (i_Draft.UdpLocalPort != c.Udp.LocalPort) { return false; }

        // TCP
        if (i_Draft.TcpHost != c.Tcp.Host) { return false; }
        if (i_Draft.TcpPort != c.Tcp.Port) { return false; }

        // Serial
        if (i_Draft.SerialLineKind != c.Serial.SerialLineKind) { return false; }
        if (i_Draft.SerialComPort != c.Serial.ComPort) { return false; }
        if (i_Draft.SerialBaudRate != c.Serial.BaudRate) { return false; }

        // Playback
        if (i_Draft.PlaybackFilePath != c.Playback.FilePath) { return false; }
        if (i_Draft.PlaybackLoop != c.Playback.Loop) { return false; }
        if (i_Draft.PlaybackFrequency != c.Playback.Frequency) { return false; }

        return true;
    }

    // Checks if the text boxes contain invalid or different values than the int backing fields
    private bool HasInvalidNumericText()
    {
        if (!IsTextIntMatch(Draft.UdpRemotePortText, m_OriginalSnapshot.Connection.Udp.RemotePort)) { return true; }
        if (!IsTextIntMatch(Draft.UdpLocalPortText, m_OriginalSnapshot.Connection.Udp.LocalPort)) { return true; }
        if (!IsTextIntMatch(Draft.TcpPortText, m_OriginalSnapshot.Connection.Tcp.Port)) { return true; }
        if (!IsTextIntMatch(Draft.SerialBaudRateText, m_OriginalSnapshot.Connection.Serial.BaudRate)) { return true; }
        return false;
    }

    // Helper to check if a text input matches the int value (valid and equal)
    private static bool IsTextIntMatch(string i_Text, int i_Value)
    {
        if (string.IsNullOrWhiteSpace(i_Text)) { return false; }
        if (!int.TryParse(i_Text, out int parsed)) { return false; }

        return parsed == i_Value;
    }
    #endregion

    #region Event Handlers
    // Whenever any Draft property changes, check if we are now dirty compared to the snapshot
    private void OnDraftPropertyChanged(object? i_Sender, PropertyChangedEventArgs i_Args)
    {
        if (m_IsLoadingDraft) { return; }
        UpdateDirtyState();
    }
    #endregion
}
