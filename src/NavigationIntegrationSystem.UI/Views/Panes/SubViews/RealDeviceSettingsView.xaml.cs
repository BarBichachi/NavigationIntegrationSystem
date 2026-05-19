using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

namespace NavigationIntegrationSystem.UI.Views.Panes.SubViews;

public sealed partial class RealDeviceSettingsView : UserControl
{
    #region Properties
    public DeviceSettingsPaneViewModel ViewModel => (DeviceSettingsPaneViewModel)DataContext;
    #endregion

    #region Constructors
    public RealDeviceSettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }
    #endregion

    #region Event Handlers
    // x:Bind compiles against the ViewModel property; when the DataContext switches (e.g. a different device selected), we must re-evaluate so the new VM's values show.
    // Guard against null / wrong type: calling Bindings.Update on a null DataContext causes the codegen's TwoWay updaters to fire with stale UI state and crash (e.g. ComboBox SelectedItem briefly null -> cast to enum throws NRE).
    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is DeviceSettingsPaneViewModel)
        {
            Bindings.Update();
        }
    }
    #endregion
}
