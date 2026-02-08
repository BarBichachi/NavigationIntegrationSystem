using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Layout;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Pages;

namespace NavigationIntegrationSystem.UI.Views.Pages;

// Displays the Integration grid and binds it to the IntegrationViewModel
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

        this.Unloaded += (s, e) => ViewModel.Deinitialize();
    }
    #endregion

    #region Event Handlers
    // Routes candidate selection click to the owning row
    private void OnSourceRadioButtonClicked(object i_Sender, RoutedEventArgs i_E)
    {
        if (i_Sender is not RadioButton radioButton) { return; }
        if (radioButton.DataContext is not IntegrationSourceCandidateViewModel src) { return; }

        DependencyObject current = radioButton;
        while (current != null && current is not ListViewItem) { current = VisualTreeHelper.GetParent(current); }
        if (current is not ListViewItem listViewItem) { return; }
        if (listViewItem.Content is not IntegrationFieldRowViewModel row) { return; }

        row.SelectSource(src);
    }
    #endregion
}
