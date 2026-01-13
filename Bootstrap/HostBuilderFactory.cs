using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;
using NavigationIntegrationSystem.Infrastructure.Configuration.Paths;
using NavigationIntegrationSystem.Infrastructure.Configuration.Settings;
using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.Services.Devices;
using NavigationIntegrationSystem.Services.UI.Dialog;
using NavigationIntegrationSystem.Services.UI.Navigation;
using NavigationIntegrationSystem.UI.ViewModels;
using NavigationIntegrationSystem.UI.ViewModels.Devices;
using NavigationIntegrationSystem.UI.ViewModels.Integration;

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

                // Register core services
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<NavigationService>();

                // Register device domain
                services.AddSingleton<DeviceCatalogService>();
                services.AddSingleton(new DevicesConfigService(AppPaths.DevicesConfigPath));
                services.AddSingleton<IInsDeviceFactory, InsDeviceFactory>();

                // Configure logging
                string logRoot = PathResolver.Resolve(settings.Nis.Log.Root);
                services.AddSingleton(new LogService(logRoot, settings.Nis.Log.MaxUiEntries));
                services.AddSingleton<LogsViewModel>();

                // Register ViewModels
                services.AddSingleton<DevicesViewModel>();
                services.AddSingleton<IntegrationViewModel>();

                // Shell
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }
    #endregion
}
