using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Playback;

namespace NavigationIntegrationSystem.UI.Views.Controls;

public sealed partial class PlaybackTrayView : UserControl
{
    public PlaybackControlsViewModel ViewModel { get; }

    public PlaybackTrayView()
    {
        InitializeComponent();
        // Global tray needs a direct resolution since it sits outside any page's VM tree
        ViewModel = App.GetService<PlaybackControlsViewModel>();
    }
}