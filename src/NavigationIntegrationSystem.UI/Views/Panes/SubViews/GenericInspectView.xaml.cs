using Microsoft.UI.Xaml.Controls;

namespace NavigationIntegrationSystem.UI.Views.Panes.SubViews;

// Code-behind for the generic field-list inspect view. No logic; the UserControl inherits DataContext from the host's ContentControl, so the {Binding Fields} resolves to GenericInspectPaneViewModel.Fields at runtime
public sealed partial class GenericInspectView : UserControl
{
    public GenericInspectView()
    {
        InitializeComponent();
    }
}
