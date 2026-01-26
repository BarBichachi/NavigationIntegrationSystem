using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models.Devices;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.Devices.Runtime;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.Devices.Implementations;

// Runtime device implementation for Manual (always available, no IO)
public sealed class ManualInsDevice : InsDeviceBase
{
    #region Ctors
    public ManualInsDevice(DeviceDefinition i_Definition, DeviceConfig i_Config, ILogService i_LogService) : base(i_Definition, i_Config, i_LogService) { }
    #endregion

    #region Functions
    // Performs Manual-specific connect logic (no IO)
    protected override Task OnConnectAsync() { return Task.CompletedTask; }

    // Performs Manual-specific disconnect logic (no IO)
    protected override Task OnDisconnectAsync() { return Task.CompletedTask; }
    #endregion
}
