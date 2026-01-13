using System.Threading.Tasks;

using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Infrastructure.Logging;

namespace NavigationIntegrationSystem.Services.Devices;

// Runtime device implementation for VN310 (logic to be added later)
public sealed class Vn310InsDevice : InsDeviceBase
{
    #region Ctors
    public Vn310InsDevice(DeviceDefinition i_Definition, LogService i_LogService) : base(i_Definition, i_LogService) { }
    #endregion

    #region Functions
    // Performs VN310-specific connect logic
    protected override Task OnConnectAsync()
    {
        return Task.CompletedTask;
    }

    // Performs VN310-specific disconnect logic
    protected override Task OnDisconnectAsync()
    {
        return Task.CompletedTask;
    }
    #endregion
}
