using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Devices.Catalog;
using NavigationIntegrationSystem.Devices.Config;
using NavigationIntegrationSystem.Devices.Runtime;
using NavigationIntegrationSystem.Infrastructure.Configuration.Paths;
using NavigationIntegrationSystem.Infrastructure.Persistence.DevicesConfig;
using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.ViewModels.Devices;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices;

// Owns the Devices page state, including device cards and right-pane behavior
public sealed partial class DevicesViewModel : ObservableObject
{
    #region Private Fields
    private readonly DevicesConfigService m_ConfigService;
    private readonly DevicesConfigFile m_ConfigFile;
    private readonly ILogService m_LogService;
    private DeviceCardViewModel? m_SelectedDevice;
    private bool m_IsPaneOpen;
    private DevicesPaneMode m_PaneMode;
    private DeviceSettingsPaneViewModel? m_CurrentSettingsPane;
    private readonly IDialogService m_DialogService;
    private string m_SaveButtonText = "Save";
    private Symbol m_SaveButtonIcon = Symbol.Save;
    #endregion

    #region Properties
    public ObservableCollection<DeviceCardViewModel> Devices { get; } = new ObservableCollection<DeviceCardViewModel>();
    public DeviceCardViewModel? SelectedDevice { get => m_SelectedDevice; set => SetProperty(ref m_SelectedDevice, value);}
    public DevicesPaneMode PaneMode { get => m_PaneMode; set => SetProperty(ref m_PaneMode, value); }
    public DeviceSettingsPaneViewModel? CurrentSettingsPane { get => m_CurrentSettingsPane; set => SetProperty(ref m_CurrentSettingsPane, value); }
    public bool IsPaneOpen { get => m_IsPaneOpen; set => SetProperty(ref m_IsPaneOpen, value); }
    public string SaveButtonText { get => m_SaveButtonText; private set => SetProperty(ref m_SaveButtonText, value); }
    public Symbol SaveButtonIcon { get => m_SaveButtonIcon; private set => SetProperty(ref m_SaveButtonIcon, value); }
    #endregion

    #region Commands
    public IRelayCommand<DeviceCardViewModel> OpenSettingsCommand { get; }
    public IRelayCommand<DeviceCardViewModel> OpenInspectCommand { get; }
    public IAsyncRelayCommand SaveDevicesConfigCommand { get; }
    public IRelayCommand ApplySettingsCommand { get; }
    #endregion

    #region Ctors
    public DevicesViewModel(DeviceCatalogService i_CatalogService, DevicesConfigService i_ConfigService, ILogService i_LogService, IDialogService i_DialogService, IInsDeviceRegistry i_DeviceRegistry)
    {
        m_ConfigService = i_ConfigService;
        m_LogService = i_LogService;
        m_DialogService = i_DialogService;
        m_ConfigFile = m_ConfigService.Load();

        m_IsPaneOpen = false;
        m_PaneMode = DevicesPaneMode.None;

        BuildDeviceCards(i_CatalogService, i_DeviceRegistry);

        OpenSettingsCommand = new RelayCommand<DeviceCardViewModel>(OnOpenSettings);
        OpenInspectCommand = new RelayCommand<DeviceCardViewModel>(OnOpenInspect);
        SaveDevicesConfigCommand = new AsyncRelayCommand(OnSaveConfigAsync);
        ApplySettingsCommand = new RelayCommand(OnApplySettings);
    }

    // Builds all device cards from the catalog and config
    private void BuildDeviceCards(DeviceCatalogService i_CatalogService, IInsDeviceRegistry i_DeviceRegistry)
    {
        Devices.Clear();

        // Create a device card for each device in the catalog
        foreach (DeviceDefinition def in i_CatalogService.GetDevices())
        {
            DeviceConfig cfg = m_ConfigService.GetOrCreateDevice(m_ConfigFile, def.Type);

            // Build inspect fields
            var fields = new ObservableCollection<InspectFieldViewModel>();
            foreach (var f in def.Fields)
            {
                fields.Add(new InspectFieldViewModel(f.Key, f.DisplayName, f.Unit));
            }

            // Create runtime device instance
            IInsDevice runtimeDevice = i_DeviceRegistry.Create(def, cfg);
            var vm = new DeviceCardViewModel(cfg, m_LogService, fields, OnOpenSettingsFromCard, OnOpenInspectFromCard, runtimeDevice);

            Devices.Add(vm);
        }
    }
    #endregion

    #region Functions
    // Opens the settings pane for a selected device
    private void OnOpenSettings(DeviceCardViewModel? i_Device)
    {
        if (i_Device == null) { return; }

        SelectedDevice = i_Device;
        CurrentSettingsPane = new DeviceSettingsPaneViewModel(this, i_Device);
        PaneMode = DevicesPaneMode.Settings;
        IsPaneOpen = true;
    }

    // Opens the inspect pane for a selected device
    private void OnOpenInspect(DeviceCardViewModel? i_Device)
    {
        if (i_Device == null) { return; }

        SelectedDevice = i_Device;
        CurrentSettingsPane = null;
        PaneMode = DevicesPaneMode.Inspect;
        IsPaneOpen = true;
    }

    // Closes the right pane (public for pane VMs)
    public void ClosePane()
    {
        if ((PaneMode == DevicesPaneMode.Settings) && (SelectedDevice != null) && (CurrentSettingsPane != null) && CurrentSettingsPane.HasUnsavedChanges)
        { SelectedDevice.HasUnsavedSettings = true; }

        PaneMode = DevicesPaneMode.None;
        SelectedDevice = null;
        CurrentSettingsPane = null;
    }

    // Saves all device configuration changes to devices.json
    private async Task OnSaveConfigAsync()
    {
        m_ConfigService.Save(m_ConfigFile);
        foreach (var device in Devices) { device.HasUnsavedSettings = false; }
        m_LogService.Info(nameof(DevicesViewModel), $"Saved devices config: {AppPaths.DevicesConfigPath}");

        await ShowSavedFeedbackAsync();
    }

    // Applies current settings pane changes if available
    private void OnApplySettings()
    {
        if (CurrentSettingsPane == null) { return; }
        CurrentSettingsPane.ApplyCommand.Execute(null);
    }

    // Returns true only when the open pane is Settings and there are unsaved changes
    public bool ShouldConfirmPaneClose()
    {
        if (!IsPaneOpen) { return false; }
        if (PaneMode != DevicesPaneMode.Settings) { return false; }
        if (CurrentSettingsPane == null) { return false; }

        return CurrentSettingsPane.HasUnsavedChanges;
    }

    // Forces close after dialog decision
    public void ForceClosePaneAfterDecision(DialogCloseDecision i_Decision)
    {
        if (PaneMode != DevicesPaneMode.Settings || CurrentSettingsPane == null)
        { IsPaneOpen = false; ClosePane(); return; }

        switch (i_Decision)
        {
            case DialogCloseDecision.Apply:
                CurrentSettingsPane.Apply();
                IsPaneOpen = false;
                ClosePane();
                return;

            case DialogCloseDecision.Discard:
                CurrentSettingsPane.Discard();
                IsPaneOpen = false;
                ClosePane();
                return;

            case DialogCloseDecision.Cancel:
                return;
        }
    }

    // Shows unsaved-changes dialog and returns user decision
    public async Task<DialogCloseDecision> ConfirmCloseSettingsAsync(XamlRoot i_XamlRoot)
    {
        return await m_DialogService.ShowUnsavedChangesDialogAsync(i_XamlRoot);
    }

    // Clears selection/pane state without touching IsPaneOpen (it already changed)
    private void CleanupPaneState()
    {
        PaneMode = DevicesPaneMode.None;
        SelectedDevice = null;
        CurrentSettingsPane = null;
    }

    // Closes the settings pane after a successful Apply without triggering the unsaved-changes dialog
    public void ForceClosePaneAfterApply()
    {
        IsPaneOpen = false;
        CleanupPaneState();
    }

    // Shows a short "Saved" feedback on the Save button
    private async Task ShowSavedFeedbackAsync()
    {
        SaveButtonText = "Saved";
        SaveButtonIcon = Symbol.Accept;

        await Task.Delay(1400);

        SaveButtonText = "Save";
        SaveButtonIcon = Symbol.Save;
    }

    // Opens settings pane for a device requested by the card
    private void OnOpenSettingsFromCard(DeviceCardViewModel i_Device) { OnOpenSettings(i_Device); }

    // Opens inspect pane for a device requested by the card
    private void OnOpenInspectFromCard(DeviceCardViewModel i_Device) { OnOpenInspect(i_Device); }
    #endregion
}