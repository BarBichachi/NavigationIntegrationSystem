using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;
using NavigationIntegrationSystem.Infrastructure.Logging;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.Services.Devices;

// Runtime device implementation for TMaps100X (logic to be added later)
public sealed class Tmaps100XInsDevice : InsDeviceBase
{
    #region Ctors
    public Tmaps100XInsDevice(DeviceDefinition i_Definition, DeviceConfig i_Config, LogService i_LogService) : base(i_Definition, i_Config, i_LogService) { }
    #endregion

    #region Functions
    // Performs TMaps100X-specific connect logic
    protected override Task OnConnectAsync()
    {
        return Task.CompletedTask;
    }

    // Performs TMaps100X-specific disconnect logic
    protected override Task OnDisconnectAsync()
    {
        return Task.CompletedTask;
    }
    #endregion
}