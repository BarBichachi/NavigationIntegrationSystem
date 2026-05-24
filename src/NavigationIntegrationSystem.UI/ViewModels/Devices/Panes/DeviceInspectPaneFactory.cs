using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;

using System;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

// Creates the device-type-appropriate inspect pane VM. Mirrors DeviceSettingsPaneFactory. Adding a bespoke inspect pane = add a case here + new VM subclass + new SubViews/<Name>InspectView.xaml + matching DataTemplate in DeviceInspectPaneView.xaml. No reflection / DI registration so the switch is the single grep-able source of truth
public static class DeviceInspectPaneFactory
{
    public static DeviceInspectPaneViewModelBase Create(DeviceCardViewModel i_Device)
    {
        switch (i_Device.Type)
        {
            case DeviceType.VN310:
                return new Vn310InspectPaneViewModel(i_Device);

            case DeviceType.Tmaps100X:
                // TMAPS stays on the generic placeholder until its bespoke inspect view is built
                return new GenericInspectPaneViewModel(i_Device);

            case DeviceType.Manual:
            case DeviceType.Playback:
                // DeviceCardViewModel.IsInspectVisible already hides the Inspect button for these types. Defensive only -- not reachable from the UI
                throw new InvalidOperationException($"Device type '{i_Device.Type}' has no inspect pane.");

            default:
                throw new ArgumentOutOfRangeException(nameof(i_Device), i_Device.Type, $"No inspect pane registered for device type '{i_Device.Type}'.");
        }
    }
}
