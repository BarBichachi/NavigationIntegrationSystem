using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.UI.ViewModels.Devices;

namespace NavigationIntegrationSystem.UI.Pages;

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
}