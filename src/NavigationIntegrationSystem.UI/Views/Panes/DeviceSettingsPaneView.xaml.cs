using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.UI.ViewModels.Devices;

namespace NavigationIntegrationSystem.UI.Views.Panes;

// Settings pane view for a selected device
public sealed partial class DeviceSettingsPaneView : UserControl
{
    #region Properties
    public DeviceSettingsPaneViewModel? ViewModel { get; set; }
    #endregion

    #region Constructors
    public DeviceSettingsPaneView()
    {
        InitializeComponent();
    }
    #endregion
}