using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Devices.Runtime;
using NavigationIntegrationSystem.UI.Bootstrap;
using NavigationIntegrationSystem.UI.Services.Logging;
using NavigationIntegrationSystem.UI.Services.UI.Windowing;

using System;

namespace NavigationIntegrationSystem;

// Boots the application, builds DI host, and opens the main window
public partial class App : Application
{
    #region Private Fields
    private readonly IHost m_Host;
    private Window? m_MainWindow;
    #endregion

    #region Properties
    public IServiceProvider Services => m_Host.Services;
    #endregion

    #region Ctors
    public App()
    {
        InitializeComponent();
        m_Host = HostBuilderFactory.Build();
    }
    #endregion

    #region Overrides
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 1. Initialize Modules (Bootstrapper Pattern)
        var bootstrapper = Services.GetRequiredService<DevicesModuleBootstrapper>();
        bootstrapper.Initialize();

        // 2. Create and Register MainWindow (Provider Pattern)
        m_MainWindow = Services.GetRequiredService<MainWindow>();
        var windowProvider = Services.GetRequiredService<WindowProvider>();
        windowProvider.MainWindow = m_MainWindow;

        // 3. UI-Specific Setup
        var uiLogBuffer = Services.GetRequiredService<UiLogBuffer>();
        uiLogBuffer.AttachUiDispatcher(DispatcherQueue.GetForCurrentThread());

        // 4. Start the Host (which starts all hosted services)
        await m_Host.StartAsync();

        var log = Services.GetRequiredService<ILogService>();
        log.Info(nameof(App), "NIS Host Started");

        // 5. Show MainWindow
        m_MainWindow.Activate();

        m_MainWindow.Closed += async (s, e) =>
        {
            await m_Host.StopAsync();
            m_Host.Dispose();
        };
    }
    #endregion
}