using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;

using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;
using NavigationIntegrationSystem.Infrastructure.Configuration.Paths;
using NavigationIntegrationSystem.Infrastructure.Configuration.Settings;
using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.Services.Devices;
using NavigationIntegrationSystem.Services.UI.Dialog;
using NavigationIntegrationSystem.Services.UI.Navigation;
using NavigationIntegrationSystem.UI.ViewModels;
using NavigationIntegrationSystem.UI.ViewModels.Integration;
using NavigationIntegrationSystem.UI.ViewModels.Devices;

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
                string logRoot = PathResolver.Resolve(settings.Nis.Log.Root);
                services.AddSingleton(new LogService(logRoot, settings.Nis.Log.MaxUiEntries));

                // Register Services and ViewModels
                services.AddSingleton<NavigationService>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<DevicesViewModel>();
                services.AddSingleton(new DevicesConfigService(AppPaths.DevicesConfigPath));
                services.AddSingleton<DeviceCatalogService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IntegrationViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }
    #endregion
}
