using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.UI.ViewModels.Integration;

namespace NavigationIntegrationSystem.UI.Views.Pages;

// Displays the Fusion grid and binds it to the IntegrationViewModel
public sealed partial class IntegrationPage : Page
{
    #region Properties
    public IntegrationViewModel ViewModel { get; }
    #endregion

    #region Constructors
    public IntegrationPage()
    {
        InitializeComponent();
        ViewModel = ((App)Application.Current).Services.GetRequiredService<IntegrationViewModel>();
        ViewModel.Initialize(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
    }
    #endregion
}
