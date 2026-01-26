using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.Core.Enums;
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

    #region Event Handlers
    // Handles manual source selection via click to avoid RadioButton binding re-entrancy
    private void OnManualRadioButtonClicked(object i_Sender, RoutedEventArgs i_E)
    {
        if (i_Sender is RadioButton rb && rb.DataContext is IntegrationFieldRowViewModel row)
        {
            row.ManualSource.IsSelected = true;
        }
    }

    // Handles device source selection via click to avoid RadioButton binding re-entrancy
    private void OnDeviceRadioButtonClicked(object i_Sender, RoutedEventArgs i_E)
    {
        if (i_Sender is RadioButton rb && rb.DataContext is SourceCandidateViewModel src)
        {
            src.IsSelected = true;
        }
    }
    #endregion

    // Relays the manual "Apply to All" request to the ViewModel
    private void OnApplyManualToAllClicked(object i_Sender, RoutedEventArgs i_E)
    {
        // Calling the same logic as hardware devices, passing DeviceType.Manual
        ViewModel.ApplyDeviceToAllFields(DeviceType.Manual);
    }
}
