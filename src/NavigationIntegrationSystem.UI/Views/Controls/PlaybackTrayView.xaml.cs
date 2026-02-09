using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Playback;

namespace NavigationIntegrationSystem.UI.Views.Controls;

public sealed partial class PlaybackTrayView : UserControl
{
    public PlaybackControlsViewModel ViewModel { get; }

    public PlaybackTrayView()
    {
        InitializeComponent();
        // Resolve VM directly (or inherit from parent if structured that way, but direct is safer for Global Tray)
        ViewModel = ((App)Application.Current).Services.GetRequiredService<PlaybackControlsViewModel>();
    }
}