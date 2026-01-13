using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;

namespace NavigationIntegrationSystem.Services.Devices;

// Creates runtime device instances based on a catalog definition
public interface IInsDeviceFactory
{
    #region Functions
    // Creates a runtime device instance for a catalog definition
    IInsDevice Create(DeviceDefinition i_Definition, DeviceConfig i_Config);
    #endregion
}