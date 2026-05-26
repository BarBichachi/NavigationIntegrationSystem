using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using NavigationIntegrationSystem.Core.Enums;
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

// Common logic for any device settings pane: holds the Draft, takes/restores snapshots, tracks dirty state, validates, and saves. Subclasses add per-device knobs (e.g. file pickers, recommended-hint accessors for real INS devices). The pane host (DeviceSettingsPaneView) binds to this base type and a DataTemplate selects the matching View per concrete subclass
public abstract partial class DeviceSettingsPaneViewModelBase : ViewModelBase
{
    #region Static Lookups
    // ItemsSource references MUST be stable across VM instances. Each new VM was previously creating a fresh ObservableCollection; when the settings pane re-opened with a new VM, ComboBox saw a different collection reference, cleared its SelectedItem mid-rebuild, and the TwoWay binding's generated cast `(SerialLineKind)null` threw NullReferenceException. Sharing one immutable list per enum avoids the clear
    private static readonly IReadOnlyList<DeviceConnectionKind> s_ConnectionKinds = (DeviceConnectionKind[])Enum.GetValues(typeof(DeviceConnectionKind));
    private static readonly IReadOnlyList<SerialLineKind> s_SerialLineKinds = (SerialLineKind[])Enum.GetValues(typeof(SerialLineKind));
    // Standard RS-232/USB-serial rates. Doubling series from 300 baud up through 921600. Omits low-traffic legacy values (50/75/110/134/150/200) and non-doubling oddballs (14400/28800/76800/128000) that almost never appear in modern device ICDs
    private static readonly IReadOnlyList<int> s_SerialBaudRates = new[] { 300, 600, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };
    #endregion

    #region Properties
    public DeviceCardViewModel Device { get; }
    public DeviceSettingsDraftViewModel Draft { get; }
    public IReadOnlyList<DeviceConnectionKind> ConnectionKinds => s_ConnectionKinds;
    public IReadOnlyList<SerialLineKind> SerialLineKinds => s_SerialLineKinds;
    public IReadOnlyList<int> SerialBaudRates => s_SerialBaudRates;
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

        // 3. Save to Real Device Config. Capture the pre-save snapshot first -- ApplyConnectionChangesAfterSaveAsync needs the old vs new comparison to decide whether to cycle the device
        DeviceConfig oldSnapshot = m_OriginalSnapshot;
        Draft.ApplyTo(Device.Config);
        await m_Parent.SaveDevicesConfigCommand.ExecuteAsync(null);

        // 4. Update Snapshot (Make this the new "Clean" state)
        m_OriginalSnapshot = Device.Config.DeepClone();
        UpdateDirtyState();

        // 5. React to connection-relevant changes. Fire-and-forget so the pane closes immediately and the device cycle (or live-tune) runs in the background; the card already listens for StateChanged so the user sees the transition without the pane staying up. Subclasses override for per-device live-apply semantics (e.g. Playback frequency)
        _ = ApplyConnectionChangesAfterSaveAsync(oldSnapshot, Device.Config);

        // 6. Notify device of AutoReconnect flag changes. Without this, toggling AutoReconnect off only takes effect at the loop's next iteration check (up to 60s away mid-backoff). Calling NotifyAutoReconnectChanged cancels in-flight loops immediately on disable, or starts a fresh loop on enable-while-in-error
        if (oldSnapshot.AutoReconnect != Device.Config.AutoReconnect)
        {
            Device.Device.NotifyAutoReconnectChanged();
        }

        m_Parent.ForceClosePaneAfterApply();
        return true;
    }

    // Default behavior after Apply&Save: if anything in the Connection block changed AND the device was active (Connected or mid-Connecting), cycle it so the new settings take effect immediately. Subclasses override -- PlaybackSettingsPaneViewModel pushes Frequency/Loop changes to the playback service in-place and only cycles on CSV path change
    protected virtual async Task ApplyConnectionChangesAfterSaveAsync(DeviceConfig i_OldConfig, DeviceConfig i_NewConfig)
    {
        if (!HasConnectionChanged(i_OldConfig.Connection, i_NewConfig.Connection)) { return; }
        if (!IsDeviceActive()) { return; }
        await CycleDeviceAsync().ConfigureAwait(false);
    }

    // Disconnect then reconnect with the now-saved Config. Used by the default ApplyConnectionChangesAfterSaveAsync and any subclass override that needs the heavy cycle
    protected async Task CycleDeviceAsync()
    {
        try
        {
            await Device.Device.DisconnectAsync().ConfigureAwait(false);
        }
        catch
        {
            // Swallow: a failed disconnect should still let us attempt to connect with the new settings. The connect path will surface its own error via DeviceStatus.Error if it can't open the connection
        }
        await Device.Device.ConnectAsync().ConfigureAwait(false);
    }

    // True if the device is in a state where re-applying connection settings means cycling. Disconnected/Error states don't need cycling -- the next user-initiated Connect will pick up the new config naturally
    protected bool IsDeviceActive()
    {
        return Device.Status == DeviceStatus.Connected || Device.Status == DeviceStatus.Connecting;
    }

    // Field-by-field compare of the Connection block. Mirrors IsDraftEqualToSnapshot's coverage so the two stay in sync if the schema grows. Returns true iff any field differs
    protected static bool HasConnectionChanged(DeviceConnectionSettings i_A, DeviceConnectionSettings i_B)
    {
        if (i_A.Kind != i_B.Kind) { return true; }

        if (i_A.Udp.RemoteIp != i_B.Udp.RemoteIp) { return true; }
        if (i_A.Udp.RemotePort != i_B.Udp.RemotePort) { return true; }
        if (i_A.Udp.LocalIp != i_B.Udp.LocalIp) { return true; }
        if (i_A.Udp.LocalPort != i_B.Udp.LocalPort) { return true; }

        if (i_A.Tcp.Host != i_B.Tcp.Host) { return true; }
        if (i_A.Tcp.Port != i_B.Tcp.Port) { return true; }

        if (i_A.Serial.SerialLineKind != i_B.Serial.SerialLineKind) { return true; }
        if (i_A.Serial.ComPort != i_B.Serial.ComPort) { return true; }
        if (i_A.Serial.BaudRate != i_B.Serial.BaudRate) { return true; }

        if (i_A.Playback.FilePath != i_B.Playback.FilePath) { return true; }
        if (i_A.Playback.Loop != i_B.Playback.Loop) { return true; }
        if (i_A.Playback.Frequency != i_B.Playback.Frequency) { return true; }

        return false;
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
