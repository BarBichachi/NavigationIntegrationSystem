using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.ViewModels.Devices;
using NavigationIntegrationSystem.UI.Views.Panes;

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
    // Intercepts light-dismiss close to allow unsaved-changes decision
    private async void OnPaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args)
    {
        if (!ViewModel.ShouldConfirmPaneClose())
        {
            ViewModel.ClosePane();
            return;
        }

        args.Cancel = true;

        DialogCloseDecision decision = await ViewModel.ConfirmCloseSettingsAsync(sender.XamlRoot);

        if (decision == DialogCloseDecision.Cancel) { return; }

        ViewModel.ForceClosePaneAfterDecision(decision);
    }
    #endregion
}