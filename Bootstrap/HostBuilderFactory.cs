using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;

using NavigationIntegrationSystem.Infrastructure.Configuration;
using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.Services.UI;
using NavigationIntegrationSystem.UI.ViewModels;

namespace NavigationIntegrationSystem.Bootstrap;

// Builds the application DI container and configures logging/services
public static class HostBuilderFactory
{
    #region Public Methods
    // Builds the DI host for the application
    public static IHost Build()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) => { config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false); })
            .ConfigureServices((context, services) =>
            {
                // Configure AppSettings
                var settings = new AppSettings();
                context.Configuration.GetSection("Nis").Bind(settings.Nis);
                services.AddSingleton(settings);

                // Configure Logging
                DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                string logRoot = PathResolver.Resolve(settings.Nis.Log.Root);
                services.AddSingleton(new LogService(logRoot, settings.Nis.Log.MaxUiEntries, dispatcherQueue));

                // Register Services and ViewModels
                services.AddSingleton<NavigationService>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }
    #endregion
}
