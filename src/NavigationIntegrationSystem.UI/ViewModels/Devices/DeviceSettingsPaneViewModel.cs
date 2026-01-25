using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using NavigationIntegrationSystem.Devices.Config;
using NavigationIntegrationSystem.Devices.Config.Enums;
using NavigationIntegrationSystem.Devices.Config.Validation;
using NavigationIntegrationSystem.UI.ViewModels.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices;

// ViewModel for the settings pane of a selected device (draft-based)
public sealed class DeviceSettingsPaneViewModel : ViewModelBase
{
    #region Private Fields
    private readonly DevicesViewModel m_Parent;
    private readonly DeviceCardViewModel m_Device;
    private readonly DeviceConfig m_OriginalSnapshot;
    private bool m_HasUnsavedChanges;
    private bool m_IsLoadingDraft;
    private XamlRoot? m_XamlRoot;
    #endregion

    #region Properties
    public DeviceCardViewModel Device { get => m_Device; }
    public DeviceSettingsDraftViewModel Draft { get; }

    public bool HasUnsavedChanges { get => m_HasUnsavedChanges; private set => SetProperty(ref m_HasUnsavedChanges, value); }
    #endregion

    #region Commands
    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand DiscardCommand { get; }
    #endregion

    #region Ctors
    public DeviceSettingsPaneViewModel(DevicesViewModel i_Parent, DeviceCardViewModel i_Device)
    {
        m_Parent = i_Parent;
        m_Device = i_Device;

        Draft = new DeviceSettingsDraftViewModel();

        ApplyCommand = new RelayCommand(() => _ = TryApply());
        DiscardCommand = new RelayCommand(OnDiscard);

        m_OriginalSnapshot = m_Device.Config.DeepClone();

        LoadDraftFromSnapshot();

        Draft.PropertyChanged += OnDraftPropertyChanged;

        HasUnsavedChanges = false;
        m_Device.HasUnsavedSettings = false;
    }
    #endregion

    #region Functions
    // Receives XamlRoot from the view (for dialogs)
    public void SetXamlRoot(XamlRoot i_XamlRoot) { m_XamlRoot = i_XamlRoot; }

    // Applies draft into real config only if valid, returns success
    public bool TryApply()
    {
        var tempConfig = m_OriginalSnapshot.DeepClone();
        Draft.ApplyTo(tempConfig);

        var errors = ConnectionSettingsValidator.Validate(tempConfig.Connection);

        if (errors.Count > 0)
        {
            LogValidationFailure(errors);
            ShowValidationSummary(errors);
            return false;
        }

        Draft.ApplyTo(m_Device.Config);

        m_Parent.SaveDevicesConfigCommand.Execute(null);

        HasUnsavedChanges = false;
        m_Device.HasUnsavedSettings = false;

        m_Parent.ForceClosePaneAfterApply();
        return true;
    }

    // Discards draft changes and restores from original snapshot
    public void Discard()
    {
        LoadDraftFromSnapshot();

        HasUnsavedChanges = false;
        m_Device.HasUnsavedSettings = false;
    }
    #endregion

    #region Private Functions
    // Loads the draft from the original snapshot without triggering dirty
    private void LoadDraftFromSnapshot()
    {
        m_IsLoadingDraft = true;
        Draft.LoadFrom(m_OriginalSnapshot);
        m_IsLoadingDraft = false;
    }

    // Handles draft changes and updates dirty state based on snapshot-diff
    private void OnDraftPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (m_IsLoadingDraft) { return; }
        UpdateDirtyState();
    }

    // Dirty = Draft-applied config differs from original snapshot
    private void UpdateDirtyState()
    {
        var tempConfig = m_OriginalSnapshot.DeepClone();
        Draft.ApplyTo(tempConfig);

        bool isDirty = !AreEquivalent(tempConfig, m_OriginalSnapshot);

        HasUnsavedChanges = isDirty;
        m_Device.HasUnsavedSettings = isDirty;
    }

    // Compares only what we persist for this device (expand later if needed)
    private static bool AreEquivalent(DeviceConfig i_A, DeviceConfig i_B)
    {
        if (i_A.AutoReconnect != i_B.AutoReconnect) { return false; }

        if (i_A.Connection.Kind != i_B.Connection.Kind) { return false; }

        // UDP
        if (i_A.Connection.Udp.RemoteIp != i_B.Connection.Udp.RemoteIp) { return false; }
        if (i_A.Connection.Udp.RemotePort != i_B.Connection.Udp.RemotePort) { return false; }
        if (i_A.Connection.Udp.LocalIp != i_B.Connection.Udp.LocalIp) { return false; }
        if (i_A.Connection.Udp.LocalPort != i_B.Connection.Udp.LocalPort) { return false; }

        // TCP
        if (i_A.Connection.Tcp.Host != i_B.Connection.Tcp.Host) { return false; }
        if (i_A.Connection.Tcp.Port != i_B.Connection.Tcp.Port) { return false; }

        // Serial
        if (i_A.Connection.Serial.SerialLineKind != i_B.Connection.Serial.SerialLineKind) { return false; }
        if (i_A.Connection.Serial.ComPort != i_B.Connection.Serial.ComPort) { return false; }
        if (i_A.Connection.Serial.BaudRate != i_B.Connection.Serial.BaudRate) { return false; }

        return true;
    }

    // Logs validation errors once per apply attempt
    private void LogValidationFailure(IReadOnlyList<string> i_Errors)
    {
        string summary = string.Join("; ", i_Errors);
        m_Device.LogService.Warn(nameof(DeviceSettingsPaneViewModel), $"Validation failed for {m_Device.DisplayName}: {summary}");
    }

    // Shows a short dialog summary (first error + count)
    private async void ShowValidationSummary(IReadOnlyList<string> i_Errors)
    {
        if (m_XamlRoot == null) { return; }

        string first = i_Errors.FirstOrDefault() ?? "Invalid settings";
        string summary = i_Errors.Count == 1 ? first : $"{first}\n(+{i_Errors.Count - 1} more)";

        await m_Parent.ShowValidationFailedAsync(m_XamlRoot, summary);
    }

    private void OnDiscard() { Discard(); }
    #endregion
}