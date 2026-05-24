using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

namespace NavigationIntegrationSystem.UI.Views.Panes;

// Host view for the per-device inspect panes. The actual inspect content comes from a DataTemplate selected by InspectPaneTemplateSelector based on the runtime VM subclass -- exactly the same shape as DeviceSettingsPaneView from Phase 5.5. Uses DataContext-based binding (matching DeviceSettingsPaneView's proven pattern)
public sealed partial class DeviceInspectPaneView : UserControl
{
    #region Properties
    public DeviceInspectPaneViewModelBase? ViewModel => DataContext as DeviceInspectPaneViewModelBase;
    #endregion

    #region Constructors
    public DeviceInspectPaneView()
    {
        InitializeComponent();
        DataContextChanged += (i_Sender, i_Args) => Bindings.Update();
    }
    #endregion
}
