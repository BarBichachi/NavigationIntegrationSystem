using Microsoft.UI.Xaml;

namespace NavigationIntegrationSystem.UI.Services.UI.Windowing;

// Contract for accessing the main application window lazily
public interface IWindowProvider
{
    Window MainWindow { get; }
}