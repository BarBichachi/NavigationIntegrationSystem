using Microsoft.Extensions.Hosting;
using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.Services.Devices;

// IHostedService that disconnects all still-active devices at app shutdown. Without this, devices that the user left Connected when closing the app would leak their OS resources (serial ports especially -- vendor SDKs commonly keep their I/O thread alive until the process actually dies, leaving the COM port "busy" for several seconds even after a new app instance starts). Runs in StopAsync before the DI container disposes the services. Lives in the UI project (not Devices) because hosted-service plumbing is a hosting-layer concern; the Devices project stays framework-agnostic, matching how IntegrationSnapshotService is wired
public sealed class InsDevicesShutdownService : IHostedService
{
    #region Private Fields
    private readonly IInsDeviceInstanceProvider m_Provider;
    private readonly ILogService m_LogService;
    #endregion

    #region Constructors
    public InsDevicesShutdownService(IInsDeviceInstanceProvider i_Provider, ILogService i_LogService)
    {
        m_Provider = i_Provider;
        m_LogService = i_LogService;
    }
    #endregion

    #region Functions
    public Task StartAsync(CancellationToken i_CancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken i_CancellationToken)
    {
        IReadOnlyList<IInsDevice> devices = m_Provider.GetAllDevices();
        foreach (IInsDevice device in devices)
        {
            if (device.Status == DeviceStatus.Disconnected) { continue; }
            try
            {
                m_LogService.Info(nameof(InsDevicesShutdownService), $"Disconnecting {device.Definition.DisplayName} on shutdown");
                await device.DisconnectAsync().ConfigureAwait(false);
            }
            catch
            {
                // Shutdown path: don't let one device's broken disconnect block the others
            }
        }
    }
    #endregion
}
