using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Devices.Catalog;
using NavigationIntegrationSystem.Devices.Modules;
using NavigationIntegrationSystem.Devices.Runtime;
using NavigationIntegrationSystem.Infrastructure.Configuration.Paths;
using NavigationIntegrationSystem.Infrastructure.Configuration.Settings;
using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.Infrastructure.Persistence.DevicesConfig;
using NavigationIntegrationSystem.UI.Services.Logging;
using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.Services.UI.Navigation;
using NavigationIntegrationSystem.UI.ViewModels;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Pages;

namespace NavigationIntegrationSystem.UI.Bootstrap;

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

                // Configure logging
                string logRoot = PathResolver.Resolve(settings.Nis.Log.Root);
                services.AddSingleton<ILogService>(sp => new FileLogService(logRoot));
                services.AddSingleton<ILogPaths>(sp => (ILogPaths)sp.GetRequiredService<ILogService>());
                services.AddSingleton(sp => new UiLogBuffer(sp.GetRequiredService<ILogService>(), settings.Nis.Log.MaxUiEntries));
                services.AddSingleton<LogsViewModel>();

                // Register core services
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<NavigationService>();

                // Register device domain
                services.AddSingleton<IInsDeviceRegistry, InsDeviceRegistry>();
                services.AddSingleton<DeviceCatalogService>();
                services.AddSingleton(new DevicesConfigService(AppPaths.DevicesConfigPath));

                // Register device modules
                services.AddSingleton<IInsDeviceModule, Vn310DeviceModule>();
                services.AddSingleton<IInsDeviceModule, Tmaps100XDeviceModule>();
                services.AddSingleton<IInsDeviceModule, ManualDeviceModule>();

                // Bootstrap device modules into registry
                services.AddSingleton<DevicesModuleBootstrapper>();

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
