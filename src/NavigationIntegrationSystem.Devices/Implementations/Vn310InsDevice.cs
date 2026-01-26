using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models.Devices;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.Devices.Runtime;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.Devices.Implementations;

// Runtime device implementation for VN310 (logic to be added later)
public sealed class Vn310InsDevice : InsDeviceBase
{
    #region Ctors
    public Vn310InsDevice(DeviceDefinition i_Definition, DeviceConfig i_Config, ILogService i_LogService) : base(i_Definition, i_Config, i_LogService) { }
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
