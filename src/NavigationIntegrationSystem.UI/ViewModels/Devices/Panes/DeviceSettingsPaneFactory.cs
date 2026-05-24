using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Playback;
using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.Services.UI.FilePicking;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;

using System;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

// Creates the device-type-appropriate settings pane VM. Adding a new bespoke pane = add a case here + new VM subclass + new View + matching DataTemplate in DeviceSettingsPaneView.xaml. No reflection / DI registration -- the switch is the single source of truth and grep-ably enumerates supported device types
public static class DeviceSettingsPaneFactory
{
    public static DeviceSettingsPaneViewModelBase Create(DevicesViewModel i_Parent, DeviceCardViewModel i_Device,
        IDialogService i_DialogService, IFilePickerService i_FilePickerService, IPlaybackService i_PlaybackService)
    {
        switch (i_Device.Type)
        {
            case DeviceType.Playback:
                return new PlaybackSettingsPaneViewModel(i_Parent, i_Device, i_DialogService, i_FilePickerService, i_PlaybackService);

            case DeviceType.VN310:
                return new Vn310SettingsPaneViewModel(i_Parent, i_Device, i_DialogService);

            case DeviceType.Tmaps100X:
                return new RealDeviceSettingsPaneViewModel(i_Parent, i_Device, i_DialogService);

            case DeviceType.Manual:
                // Manual has no connection settings and DeviceCardViewModel.IsSettingsVisible already hides the Settings button. This branch is defensive only -- it should not be reached from the UI
                throw new InvalidOperationException("Manual devices have no settings pane.");

            default:
                throw new ArgumentOutOfRangeException(nameof(i_Device), i_Device.Type, $"No settings pane registered for device type '{i_Device.Type}'.");
        }
    }
}
