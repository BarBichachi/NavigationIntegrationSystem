using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Settings;

namespace NavigationIntegrationSystem.UI.Views.Pages;

public sealed partial class SettingsPage : Page
{
    #region Properties
    public SettingsViewModel ViewModel { get; }
    #endregion

    #region Constructors
    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<SettingsViewModel>();
    }
    #endregion
}