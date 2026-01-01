using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;
using NavigationIntegrationSystem.Infrastructure.Configuration.Paths;
using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.Services.Devices;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices;

// Owns the Devices page state, including device cards and right-pane behavior
public sealed partial class DevicesViewModel : ObservableObject
{
    #region Private Fields
    private readonly DevicesConfigService m_ConfigService;
    private readonly DevicesConfigFile m_ConfigFile;
    private readonly LogService m_LogService;
    private DeviceCardViewModel? m_SelectedDevice;
    private bool m_IsPaneOpen;
    private DevicesPaneMode m_PaneMode;
    #endregion

    #region Properties
    public ObservableCollection<DeviceCardViewModel> Devices { get; } = new ObservableCollection<DeviceCardViewModel>();
    public DeviceCardViewModel? SelectedDevice
    {
        get => m_SelectedDevice;
        set => SetProperty(ref m_SelectedDevice, value);
    }
    public bool IsPaneOpen
    {
        get => m_IsPaneOpen;
        set => SetProperty(ref m_IsPaneOpen, value);
    }
    public DevicesPaneMode PaneMode
    {
        get => m_PaneMode;
        set => SetProperty(ref m_PaneMode, value);
    }
    #endregion

    #region Commands
    public IRelayCommand<DeviceCardViewModel> OpenSettingsCommand { get; }
    public IRelayCommand<DeviceCardViewModel> OpenInspectCommand { get; }
    public IRelayCommand ClosePaneCommand { get; }
    public IRelayCommand SaveDevicesConfigCommand { get; }
    #endregion

    #region Ctors
    public DevicesViewModel(DeviceCatalogService i_CatalogService, DevicesConfigService i_ConfigService, LogService i_LogService)
    {
        m_ConfigService = i_ConfigService;
        m_LogService = i_LogService;
        m_IsPaneOpen = false;
        m_PaneMode = DevicesPaneMode.None;

        m_ConfigFile = m_ConfigService.Load();

        foreach (var def in i_CatalogService.GetDevices())
        {
            DeviceConfig cfg = m_ConfigService.GetOrCreateDevice(m_ConfigFile, def.DeviceId);

            var fields = new ObservableCollection<InspectFieldViewModel>();
            foreach (var f in def.Fields)
            { fields.Add(new InspectFieldViewModel(f.Key, f.DisplayName, f.Unit)); }

            var vm = new DeviceCardViewModel(def.DeviceId, def.DisplayName, def.Type, cfg, m_LogService, fields, OnOpenSettingsFromCard, OnOpenInspectFromCard);
            Devices.Add(vm);
        }

        OpenSettingsCommand = new RelayCommand<DeviceCardViewModel>(OnOpenSettings);
        OpenInspectCommand = new RelayCommand<DeviceCardViewModel>(OnOpenInspect);
        ClosePaneCommand = new RelayCommand(OnClosePane);
        SaveDevicesConfigCommand = new RelayCommand(OnSaveConfig);
    }
    #endregion

    #region Functions
    // Opens the settings pane for a selected device
    private void OnOpenSettings(DeviceCardViewModel? i_Device)
    {
        if (i_Device == null) { return; }

        SelectedDevice = i_Device;
        PaneMode = DevicesPaneMode.Settings;
        IsPaneOpen = true;
    }

    // Opens the inspect pane for a selected device
    private void OnOpenInspect(DeviceCardViewModel? i_Device)
    {
        if (i_Device == null) { return; }

        SelectedDevice = i_Device;
        PaneMode = DevicesPaneMode.Inspect;
        IsPaneOpen = true;
    }

    // Closes the right pane
    private void OnClosePane()
    {
        IsPaneOpen = false;
        PaneMode = DevicesPaneMode.None;
        SelectedDevice = null;
    }

    // Saves all device configuration changes to devices.json
    private void OnSaveConfig()
    {
        foreach (var d in Devices)
        { d.SyncAutoReconnectToConfig(); }

        m_ConfigService.Save(m_ConfigFile);
        m_LogService.Info(nameof(DevicesViewModel), $"Saved devices config: {AppPaths.DevicesConfigPath}");
    }

    // Opens settings pane for a device requested by the card
    private void OnOpenSettingsFromCard(DeviceCardViewModel i_Device) { OnOpenSettings(i_Device); }

    // Opens inspect pane for a device requested by the card
    private void OnOpenInspectFromCard(DeviceCardViewModel i_Device) { OnOpenInspect(i_Device); }
    #endregion
}