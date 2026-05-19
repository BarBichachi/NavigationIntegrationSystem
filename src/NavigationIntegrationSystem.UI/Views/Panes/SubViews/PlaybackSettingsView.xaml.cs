using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

namespace NavigationIntegrationSystem.UI.Views.Panes.SubViews;

public sealed partial class PlaybackSettingsView : UserControl
{
    #region Properties
    public DeviceSettingsPaneViewModel ViewModel => (DeviceSettingsPaneViewModel)DataContext;
    #endregion

    #region Constructors
    public PlaybackSettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }
    #endregion

    #region Event Handlers
    // x:Bind compiles against the ViewModel property; when the DataContext switches (e.g. a different device selected), we must re-evaluate so the new VM's values show
    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();
    }
    #endregion
}
