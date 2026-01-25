using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.UI.ViewModels.Devices;

namespace NavigationIntegrationSystem.UI.Views.Panes;

// Settings pane view for a selected device
public sealed partial class DeviceSettingsPaneView : UserControl
{
    #region Constructors
    public DeviceSettingsPaneView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    #endregion

    #region Functions
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DeviceSettingsPaneViewModel vm) { vm.SetXamlRoot(XamlRoot); }
    }
    #endregion
}