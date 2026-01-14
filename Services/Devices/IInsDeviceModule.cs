using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Infrastructure.Logging;

namespace NavigationIntegrationSystem.Services.Devices;

// Provides device definition and registers runtime device creation
public interface IInsDeviceModule
{
    #region Properties
    DeviceType Type { get; }
    #endregion

    #region Functions
    // Builds the device catalog definition
    DeviceDefinition BuildDefinition();

    // Registers runtime device creation for this module
    void Register(IInsDeviceRegistry i_Registry, LogService i_LogService);
    #endregion
}
