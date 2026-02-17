using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models.Devices;
using NavigationIntegrationSystem.Core.Playback;
using NavigationIntegrationSystem.Devices.Catalog;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.Devices.Runtime;
using NavigationIntegrationSystem.Infrastructure.Configuration.Paths;
using NavigationIntegrationSystem.Infrastructure.Persistence.DevicesConfig;
using NavigationIntegrationSystem.UI.Enums;
using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.Services.UI.FilePicking;
using NavigationIntegrationSystem.UI.ViewModels;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;

// Owns the Devices page state, including device cards and right-pane behavior
public sealed partial class DevicesViewModel : ObservableObject
{
    #region Private Fields
    private readonly DevicesConfigService m_ConfigService;
    private readonly DevicesConfigFile m_ConfigFile;
    private readonly ILogService m_LogService;
    private readonly IFilePickerService m_FilePickerService;
    private readonly IPlaybackService m_PlaybackService;
    private readonly DeviceCatalogService m_CatalogService;
    private DeviceCardViewModel? m_SelectedDevice;
    private bool m_IsPaneOpen;
    private DevicesPaneMode m_PaneMode;
    private DeviceSettingsPaneViewModel? m_CurrentSettingsPane;
    private readonly IDialogService m_DialogService;
    private readonly MainViewModel m_MainViewModel;
    private static readonly int[] m_PlaybackFrequencies = new[] { 10, 25, 50, 100 };
    #endregion

    #region Properties
    public ObservableCollection<DeviceCardViewModel> Devices { get; } = new ObservableCollection<DeviceCardViewModel>();
    public DeviceCardViewModel? SelectedDevice { get => m_SelectedDevice; set => SetProperty(ref m_SelectedDevice, value);}
    public DevicesPaneMode PaneMode { get => m_PaneMode; set => SetProperty(ref m_PaneMode, value); }
    public DeviceSettingsPaneViewModel? CurrentSettingsPane { get => m_CurrentSettingsPane; set => SetProperty(ref m_CurrentSettingsPane, value); }
    // Exposes the current devices.json path for footer binding
    public string DevicesConfigPath => AppPaths.DevicesConfigPath;
    public bool IsPaneOpen
    {
        get => m_IsPaneOpen;
        set
        {
            if (SetProperty(ref m_IsPaneOpen, value)) { m_MainViewModel.IsDevicePaneOpen = value; }
        }
    }
    #endregion

    #region Commands
    public IRelayCommand<DeviceCardViewModel> OpenSettingsCommand { get; }
    public IRelayCommand<DeviceCardViewModel> OpenInspectCommand { get; }
    public IAsyncRelayCommand SaveDevicesConfigCommand { get; }
    public IAsyncRelayCommand ImportDevicesConfigCommand { get; }
    public IAsyncRelayCommand ExportDevicesConfigCommand { get; }
    public IAsyncRelayCommand OpenDevicesConfigFolderCommand { get; }
    #endregion

    #region Ctors
    public DevicesViewModel(DeviceCatalogService i_CatalogService, DevicesConfigService i_ConfigService, ILogService i_LogService,
        IDialogService i_DialogService, IInsDeviceRegistry i_DeviceRegistry, IFilePickerService i_FilePickerService, IPlaybackService i_PlaybackService,
        MainViewModel i_MainViewModel)
    {
        m_CatalogService = i_CatalogService;
        m_ConfigService = i_ConfigService;
        m_LogService = i_LogService;
        m_DialogService = i_DialogService;
        m_ConfigFile = m_ConfigService.Load();
        m_FilePickerService = i_FilePickerService;
        m_PlaybackService = i_PlaybackService;
        m_MainViewModel = i_MainViewModel;

        m_IsPaneOpen = false;
        m_PaneMode = DevicesPaneMode.None;

        BuildDeviceCards(i_CatalogService, i_DeviceRegistry);

        OpenSettingsCommand = new RelayCommand<DeviceCardViewModel>(OnOpenSettings);
        OpenInspectCommand = new RelayCommand<DeviceCardViewModel>(OnOpenInspect);
        SaveDevicesConfigCommand = new AsyncRelayCommand(OnSaveConfigAsync);
        ImportDevicesConfigCommand = new AsyncRelayCommand(OnImportDevicesConfigAsync);
        ExportDevicesConfigCommand = new AsyncRelayCommand(OnExportDevicesConfigAsync);
        OpenDevicesConfigFolderCommand = new AsyncRelayCommand(OnOpenDevicesConfigFolderAsync);
    }

    // Builds all device cards from the catalog and config
    private void BuildDeviceCards(DeviceCatalogService i_CatalogService, IInsDeviceRegistry i_DeviceRegistry)
    {
        Devices.Clear();

        // Create a device card for each device in the catalog
        foreach (DeviceDefinition def in i_CatalogService.GetDevices().OrderByDescending(d => d.Type == DeviceType.Manual).ThenBy(d => d.Type))
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
            var vm = new DeviceCardViewModel(cfg, m_LogService, m_DialogService, fields, OnOpenSettingsFromCard, OnOpenInspectFromCard, runtimeDevice);

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
        CurrentSettingsPane = new DeviceSettingsPaneViewModel(this, i_Device, m_FilePickerService, m_PlaybackService, m_DialogService);
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

    // Saves all device configuration changes to devices.json
    private async Task OnSaveConfigAsync()
    {
        await SaveConfigInternalAsync(false);
    }

    // Imports devices configuration from a JSON file
    private async Task OnImportDevicesConfigAsync()
    {
        if (IsPaneOpen)
        {
            bool allowed = await RequestPaneCloseAsync();
            if (!allowed) { return; }
        }

        string? path = await m_FilePickerService.PickSingleFileAsync(new[] { ".json" });
        if (string.IsNullOrWhiteSpace(path)) { return; }

        DevicesConfigImportResult result = m_ConfigService.ImportFromFile(path, m_CatalogService.GetDevices().Select(device => device.Type), m_PlaybackFrequencies);
        if (!result.IsSuccess || result.Config == null)
        {
            m_LogService.Error(nameof(DevicesViewModel), $"Import failed: {result.Message}");
            await m_DialogService.ShowErrorAsync("Import Failed", result.Message);
            return;
        }

        m_ConfigService.ApplyImportedConfig(m_ConfigFile, result.Config);

        bool saved = await SaveConfigInternalAsync(true);
        if (!saved) { return; }

        m_LogService.Info(nameof(DevicesViewModel), $"Imported devices config: {path}");
        await m_DialogService.ShowInfoAsync("Import Complete", "Device settings were imported successfully.");
    }

    // Exports devices configuration to a JSON file
    private async Task OnExportDevicesConfigAsync()
    {
        string? path = await m_FilePickerService.PickSaveFileAsync("devices.json", new Dictionary<string, IList<string>> { { "JSON", new[] { ".json" } } });
        if (string.IsNullOrWhiteSpace(path)) { return; }

        DevicesConfigExportResult result = m_ConfigService.ExportToFile(m_ConfigFile, path);
        if (!result.IsSuccess)
        {
            m_LogService.Error(nameof(DevicesViewModel), $"Export failed: {result.Message}");
            await m_DialogService.ShowErrorAsync("Export Failed", result.Message);
            return;
        }

        m_LogService.Info(nameof(DevicesViewModel), $"Exported devices config: {path}");
        await m_DialogService.ShowInfoAsync("Export Complete", "Device settings were exported successfully.");
    }

    // Opens the folder containing devices.json
    private async Task OnOpenDevicesConfigFolderAsync()
    {
        string? folderPath = Path.GetDirectoryName(AppPaths.DevicesConfigPath);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            await m_DialogService.ShowErrorAsync("Open Folder Failed", "Devices configuration folder path is invalid.");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true };
            Process.Start(startInfo);
        }
        catch
        {
            await m_DialogService.ShowErrorAsync("Open Folder Failed", "Unable to open the devices configuration folder.");
        }
    }

    // Saves devices configuration and returns whether it succeeded
    private async Task<bool> SaveConfigInternalAsync(bool i_ShowErrors)
    {
        DevicesConfigExportResult result = m_ConfigService.SaveWithResult(m_ConfigFile);
        if (!result.IsSuccess)
        {
            m_LogService.Error(nameof(DevicesViewModel), $"Save failed: {result.Message}");
            if (i_ShowErrors)
            {
                await m_DialogService.ShowErrorAsync("Save Failed", result.Message);
            }
            return false;
        }

        foreach (DeviceCardViewModel device in Devices) { device.HasUnsavedSettings = false; }
        m_LogService.Info(nameof(DevicesViewModel), $"Saved devices config: {AppPaths.DevicesConfigPath}");
        return true;
    }

    // Shows validation-failed dialog with summary
    public Task ShowValidationFailedAsync(XamlRoot i_XamlRoot, string i_Summary)
    {
        return m_DialogService.ShowValidationFailedDialogAsync(i_XamlRoot, i_Summary);
    }

    // Clears selection/pane state without touching IsPaneOpen (it already changed)
    private void CleanupPaneState()
    {
        PaneMode = DevicesPaneMode.None;
        SelectedDevice = null;
        CurrentSettingsPane = null;
    }

    // Requests pane close with confirmation if there are unsaved changes. Returns true if the pane can be closed.
    public async Task<bool> RequestPaneCloseAsync()
    {
        if (!IsPaneOpen) return true;

        if (CurrentSettingsPane != null)
        {
            // The Pane owns the logic. We just await the result.
            bool allowed = await CurrentSettingsPane.CanCloseAsync();
            if (!allowed) return false;
        }

        ClosePane();
        return true;
    }

    // Closes the pane without confirmation (used when the pane itself requests close, so we skip the dialog)
    public void ClosePane()
    {
        CurrentSettingsPane = null;
        IsPaneOpen = false;
        PaneMode = DevicesPaneMode.None;
        SelectedDevice = null;
    }

    // Forces pane close after user makes a decision in the unsaved-changes dialog
    public void ForceClosePaneAfterApply()
    {
        ClosePane();
    }

    // Opens settings pane for a device requested by the card
    private void OnOpenSettingsFromCard(DeviceCardViewModel i_Device) { OnOpenSettings(i_Device); }

    // Opens inspect pane for a device requested by the card
    private void OnOpenInspectFromCard(DeviceCardViewModel i_Device) { OnOpenInspect(i_Device); }
    #endregion
}