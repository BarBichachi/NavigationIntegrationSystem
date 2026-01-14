using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;
using System;

namespace NavigationIntegrationSystem.Services.Devices;

// Holds runtime creators for each supported device type
public interface IInsDeviceRegistry
{
    #region Functions
    // Registers a device factory for a given device type
    void Register(DeviceType i_Type, Func<DeviceDefinition, DeviceConfig, IInsDevice> i_Factory);

    // Creates a device instance for a given definition/config
    IInsDevice Create(DeviceDefinition i_Definition, DeviceConfig i_Config);
    #endregion
}