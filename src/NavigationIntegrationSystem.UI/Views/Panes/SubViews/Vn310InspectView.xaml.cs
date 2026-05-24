using Microsoft.UI.Xaml.Controls;

namespace NavigationIntegrationSystem.UI.Views.Panes.SubViews;

// Code-behind for the VN310-specific inspect view. No logic; the UserControl inherits DataContext from the host's ContentControl, so {Binding X} resolves to Vn310InspectPaneViewModel.X at runtime
public sealed partial class Vn310InspectView : UserControl
{
    public Vn310InspectView()
    {
        InitializeComponent();
    }
}
