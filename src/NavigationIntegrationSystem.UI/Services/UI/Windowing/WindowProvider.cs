using Microsoft.UI.Xaml;

namespace NavigationIntegrationSystem.UI.Services.UI.Windowing;

// Singleton container for the MainWindow reference
public sealed class WindowProvider : IWindowProvider
{
    private Window? m_MainWindow;

    public Window MainWindow
    {
        get => m_MainWindow ?? throw new System.InvalidOperationException("MainWindow has not been initialized yet.");
        set => m_MainWindow = value;
    }
}