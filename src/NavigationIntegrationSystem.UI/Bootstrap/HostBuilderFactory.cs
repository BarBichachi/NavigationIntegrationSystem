using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Playback;
using NavigationIntegrationSystem.Core.Recording;
using NavigationIntegrationSystem.Devices.Catalog;
using NavigationIntegrationSystem.Devices.Modules;
using NavigationIntegrationSystem.Devices.Runtime;
using NavigationIntegrationSystem.Infrastructure.Configuration.Paths;
using NavigationIntegrationSystem.Infrastructure.Configuration.Settings;
using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.Infrastructure.Persistence.DevicesConfig;
using NavigationIntegrationSystem.Infrastructure.Playback;
using NavigationIntegrationSystem.Infrastructure.Recording;
using NavigationIntegrationSystem.UI.Services.Logging;
using NavigationIntegrationSystem.UI.Services.Recording;
using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.Services.UI.FilePicking;
using NavigationIntegrationSystem.UI.Services.UI.Navigation;
using NavigationIntegrationSystem.UI.Services.UI.Windowing;
using NavigationIntegrationSystem.UI.ViewModels;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Pages;
using NavigationIntegrationSystem.UI.ViewModels.Playback;
using NavigationIntegrationSystem.UI.ViewModels.Settings;

namespace NavigationIntegrationSystem.UI.Bootstrap;

public static class HostBuilderFactory
{
    #region Public Methods
    public static IHost Build()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                // 1. Configuration & Persistence
                var settings = new AppSettings();
                context.Configuration.GetSection("Nis").Bind(settings.Nis);
                services.AddSingleton(settings);
                services.AddSingleton(new DevicesConfigService(AppPaths.DevicesConfigPath));

                // 2. Logging Infrastructure
                string logRoot = PathResolver.Resolve(settings.Nis.Log.Root);
                services.AddSingleton<ILogService>(sp => new FileLogService(logRoot));
                services.AddSingleton<ILogPaths>(sp => (ILogPaths)sp.GetRequiredService<ILogService>());
                services.AddSingleton(sp => new UiLogBuffer(sp.GetRequiredService<ILogService>(), settings.Nis.Log.MaxUiEntries));

                // 3. Playback Services
                services.AddSingleton<IPlaybackService, CsvPlaybackService>();

                // 4. Device Domain & Management
                services.AddSingleton<InsDeviceRegistry>();
                services.AddSingleton<IInsDeviceRegistry>(sp => sp.GetRequiredService<InsDeviceRegistry>());
                services.AddSingleton<IInsDeviceInstanceProvider>(sp => sp.GetRequiredService<InsDeviceRegistry>());
                services.AddSingleton<DeviceCatalogService>();

                // 5. Device Modules
                services.AddSingleton<IInsDeviceModule, Vn310DeviceModule>();
                services.AddSingleton<IInsDeviceModule, Tmaps100XDeviceModule>();
                services.AddSingleton<IInsDeviceModule, ManualDeviceModule>();
                services.AddSingleton<IInsDeviceModule, PlaybackDeviceModule>();
                services.AddSingleton<DevicesModuleBootstrapper>();

                // 6. Recording & Snapshot Services
                services.AddSingleton<IRecordingService, NisRecordingService>();
                services.AddSingleton<CsvTestingService>();

                // HOSTED SERVICE: Starts background recording logic
                services.AddHostedService<IntegrationSnapshotService>();

                // 7. UI Services
                services.AddSingleton<WindowProvider>();
                services.AddSingleton<IWindowProvider>(sp => sp.GetRequiredService<WindowProvider>());
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<NavigationService>();
                services.AddSingleton<IFilePickerService, FilePickerService>();

                // 8. ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<DevicesViewModel>();
                services.AddSingleton<IntegrationViewModel>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<PlaybackControlsViewModel>();

                // 9. Shell
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }
    #endregion
}