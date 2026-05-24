using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

namespace NavigationIntegrationSystem.UI.Views.Panes;

// Settings pane view for a selected device. Hosts the Apply&Save footer + dirty warning and delegates per-device content to a ContentControl with a DataTemplateSelector
public sealed partial class DeviceSettingsPaneView : UserControl
{
    #region Properties
    public DeviceSettingsPaneViewModelBase ViewModel => (DeviceSettingsPaneViewModelBase)DataContext;
    #endregion

    #region Constructors
    public DeviceSettingsPaneView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }
    #endregion

    #region Event Handlers
    // Updates ViewModel with current XamlRoot when DataContext changes so the pane VM can show validation / unsaved-changes dialogs
    private void OnDataContextChanged(FrameworkElement i_Sender, DataContextChangedEventArgs i_Args)
    {
        if (i_Args.NewValue is DeviceSettingsPaneViewModelBase vm)
        {
            vm.SetXamlRoot(XamlRoot);
            Bindings.Update();
        }
    }
    #endregion
}
