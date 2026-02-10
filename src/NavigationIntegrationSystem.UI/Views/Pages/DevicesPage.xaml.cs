using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;

namespace NavigationIntegrationSystem.UI.Views.Pages;

// Displays fixed device cards and provides settings/inspect panes per device
public sealed partial class DevicesPage : Page
{
    #region Properties
    public DevicesViewModel ViewModel { get; }
    #endregion

    #region Ctors
    public DevicesPage()
    {
        InitializeComponent();
        ViewModel = ((App)Application.Current).Services.GetRequiredService<DevicesViewModel>();
    }
    #endregion

    #region Event Handlers
    // We have to handle the pane's closing event in code-behind because we need to do async checks before allowing it to close.
    private async void OnPaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args)
    {
        args.Cancel = true;

        await ViewModel.RequestPaneCloseAsync();
    }
    #endregion
}