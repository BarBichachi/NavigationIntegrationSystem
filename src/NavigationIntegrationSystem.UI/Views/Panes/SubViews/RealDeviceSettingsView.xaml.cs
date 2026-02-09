using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

namespace NavigationIntegrationSystem.UI.Views.Panes.SubViews;

public sealed partial class RealDeviceSettingsView : UserControl
{
    public DeviceSettingsPaneViewModel ViewModel => (DeviceSettingsPaneViewModel)DataContext;

    public RealDeviceSettingsView()
    {
        InitializeComponent();
    }
}