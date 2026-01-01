using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.UI.ViewModels;

namespace NavigationIntegrationSystem.UI.Pages;

// Displays the live log stream from LogsViewModel
public sealed partial class LogsPage : Page
{
    #region Properties
    public LogsViewModel ViewModel { get; }
    #endregion

    #region Ctors
    public LogsPage()
    {
        InitializeComponent();
        ViewModel = ((App)Application.Current).Services.GetRequiredService<LogsViewModel>();
    }
    #endregion
}
