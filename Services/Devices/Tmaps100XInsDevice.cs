using System.Threading.Tasks;

using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Infrastructure.Logging;

namespace NavigationIntegrationSystem.Services.Devices;

// Runtime device implementation for TMaps100X (logic to be added later)
public sealed class Tmaps100XInsDevice : InsDeviceBase
{
    #region Ctors
    public Tmaps100XInsDevice(DeviceDefinition i_Definition, LogService i_LogService) : base(i_Definition, i_LogService) { }
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