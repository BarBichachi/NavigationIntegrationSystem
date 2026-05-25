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
using System.Threading;
using System.Threading.Tasks;

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

    #region Static Service Access
    // Single resolution point for views/controls that can't be constructor-injected by the WinUI 3 framework.
    // Keeps the service-locator pattern contained behind one well-known helper.
    public static T GetService<T>() where T : notnull
    {
        return ((App)Current).Services.GetRequiredService<T>();
    }
    #endregion

    #region Ctors
    public App()
    {
        InitializeComponent();
        m_Host = HostBuilderFactory.Build();
        InstallGlobalExceptionHandlers();
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

    #region Functions
    // Wires the three .NET exception channels to ILogService so otherwise-silent failures are visible in the daily log. Does NOT prevent crashes -- in .NET 8 an unhandled exception on a background thread terminates the process regardless. Specific known case this addresses: the VectorNav SDK's HandleSerialPortNotifications thread (named "VN.SerialPort (COMx)") throws when the VN310 loses power while connected; without these handlers, the process dies with no log evidence
    private void InstallGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        UnhandledException += OnApplicationUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    // Resolves ILogService defensively. Handlers must never throw, so a failure to resolve (host disposed mid-shutdown, DI not ready, etc.) is swallowed silently
    private ILogService? TryGetLog()
    {
        try { return Services.GetService<ILogService>(); }
        catch { return null; }
    }
    #endregion

    #region Event Handlers
    // Fires for unhandled exceptions on any non-UI thread (including the VectorNav SDK's serial-notifications thread). Process WILL terminate after this returns -- we only get one shot to log post-mortem evidence. The thread-name hint helps future-us identify SDK-originated crashes immediately. Args type fully qualified because Microsoft.UI.Xaml also exports a UnhandledExceptionEventArgs and the AppDomain delegate expects the System one
    private void OnAppDomainUnhandledException(object i_Sender, System.UnhandledExceptionEventArgs i_Args)
    {
        try
        {
            Exception? ex = i_Args.ExceptionObject as Exception;
            string threadName = Thread.CurrentThread.Name ?? "(unnamed)";
            string hint = threadName.StartsWith("VN.SerialPort", StringComparison.Ordinal)
                ? " [VectorNav SDK thread -- likely VN310 power loss or cable fault]"
                : string.Empty;
            TryGetLog()?.Error(nameof(App), $"Unhandled exception on thread '{threadName}'. IsTerminating={i_Args.IsTerminating}.{hint}", ex);
        }
        catch { /* handler must not throw; process is dying */ }
    }

    // Fires for unhandled exceptions surfaced through the WinUI 3 dispatcher (UI thread, x:Bind failures, command handlers, etc.). e.Handled left at default (false) -- letting the framework terminate as-is preserves current behavior; we only gain logging visibility
    private void OnApplicationUnhandledException(object i_Sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs i_Args)
    {
        try
        {
            TryGetLog()?.Error(nameof(App), $"Unhandled UI exception: {i_Args.Message}", i_Args.Exception);
        }
        catch { /* ditto */ }
    }

    // Fires when a Task's exception is never observed (no await, no .Wait, no .Exception access). Since .NET 4.5 this no longer crashes the process by default, but logging the noise still surfaces missed try/catches in async code paths
    private void OnUnobservedTaskException(object? i_Sender, UnobservedTaskExceptionEventArgs i_Args)
    {
        try
        {
            TryGetLog()?.Error(nameof(App), "Unobserved Task exception", i_Args.Exception);
            i_Args.SetObserved();
        }
        catch { /* ditto */ }
    }
    #endregion
}