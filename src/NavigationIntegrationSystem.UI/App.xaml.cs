using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Devices.Runtime;
using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.UI.Bootstrap;
using NavigationIntegrationSystem.UI.Services.Logging;
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
    // Starts the application and opens the main window
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var uiLogBuffer = Services.GetRequiredService<UiLogBuffer>();
        uiLogBuffer.AttachUiDispatcher(DispatcherQueue.GetForCurrentThread());

        var log = Services.GetRequiredService<ILogService>();
        log.Info(nameof(App), "NIS starting");

        _ = Services.GetRequiredService<DevicesModuleBootstrapper>();

        m_MainWindow = Services.GetRequiredService<MainWindow>();
        m_MainWindow.Activate();
    }
    #endregion
}