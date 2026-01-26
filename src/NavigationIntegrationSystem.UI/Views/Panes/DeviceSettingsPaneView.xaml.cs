using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

namespace NavigationIntegrationSystem.UI.Views.Panes;

// Settings pane view for a selected device
public sealed partial class DeviceSettingsPaneView : UserControl
{
    #region Constructors
    public DeviceSettingsPaneView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }
    #endregion

    #region Functions
    // Updates ViewModel with current XamlRoot when DataContext changes
    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is DeviceSettingsPaneViewModel vm) { vm.SetXamlRoot(XamlRoot); }
    }
    #endregion
}