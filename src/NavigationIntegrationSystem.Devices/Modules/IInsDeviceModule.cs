using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Devices.Runtime;

namespace NavigationIntegrationSystem.Devices.Modules;

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
    void Register(IInsDeviceRegistry i_Registry, ILogService i_LogService);
    #endregion
}
