using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Devices.Runtime;
using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.UI.Bootstrap;
using NavigationIntegrationSystem.UI.Services.Logging;
using NavigationIntegrationSystem.UI.Services.Recording;

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
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 1. Warm up the UI Log Buffer
        var uiLogBuffer = Services.GetRequiredService<UiLogBuffer>();
        uiLogBuffer.AttachUiDispatcher(DispatcherQueue.GetForCurrentThread());

        var log = Services.GetRequiredService<ILogService>();
        log.Info(nameof(App), "NIS starting");

        // 2. Initialize device modules
        _ = Services.GetRequiredService<DevicesModuleBootstrapper>();
        
        // 3. Warm up the Recording Snapshot Service
        _ = Services.GetRequiredService<IntegrationSnapshotService>();

        // 4. Show MainWindow
        m_MainWindow = Services.GetRequiredService<MainWindow>();
        m_MainWindow.Activate();
    }
    #endregion
}