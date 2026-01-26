using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models.DeviceCatalog;
using NavigationIntegrationSystem.Core.Models.Devices;
using NavigationIntegrationSystem.Devices.Implementations;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.Devices.Runtime;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Devices.Modules;

// Manual module: definition + runtime registration
public sealed class ManualDeviceModule : IInsDeviceModule
{
    #region Properties
    public DeviceType Type => DeviceType.Manual;
    #endregion

    #region Functions
    // Builds the Manual device definition (no real fields; used as a selectable source)
    public DeviceDefinition BuildDefinition()
    {
        return new DeviceDefinition(
            i_Type: DeviceType.Manual,
            i_Fields: new List<DeviceFieldDefinition>());
    }

    // Registers runtime creation for Manual
    public void Register(IInsDeviceRegistry i_Registry, ILogService i_LogService)
    {
        i_Registry.Register(Type, (DeviceDefinition def, DeviceConfig cfg) => new ManualInsDevice(def, cfg, i_LogService));
    }
    #endregion
}
