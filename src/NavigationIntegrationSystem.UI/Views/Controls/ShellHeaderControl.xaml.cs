// FILE: src\NavigationIntegrationSystem.UI\Views\Controls\ShellHeaderControl.xaml.cs
using Microsoft.UI.Xaml.Controls;

using NavigationIntegrationSystem.UI.ViewModels;

namespace NavigationIntegrationSystem.UI.Views.Controls;

public sealed partial class ShellHeaderControl : UserControl
{
    #region Properties
    // Exposes the internal Grid to be used as the custom title bar by the Window
    public Grid TitleBarElement => AppTitleBar;

    // Type-safe access to the ViewModel for x:Bind
    public MainViewModel ViewModel => (MainViewModel)DataContext;
    #endregion

    #region Constructors
    public ShellHeaderControl()
    {
        this.InitializeComponent();
    }
    #endregion
}