using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

namespace NavigationIntegrationSystem.UI.Views.Panes.SubViews;

public sealed partial class PlaybackSettingsView : UserControl
{
    public DeviceSettingsPaneViewModel ViewModel => (DeviceSettingsPaneViewModel)DataContext;

    public PlaybackSettingsView()
    {
        InitializeComponent();
    }
}