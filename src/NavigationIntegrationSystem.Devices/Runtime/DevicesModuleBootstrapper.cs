using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Devices.Modules;
using System;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Devices.Runtime;

// Bootstraps device modules into the device registry
public sealed class DevicesModuleBootstrapper
{
    #region Ctors
    public DevicesModuleBootstrapper(IEnumerable<IInsDeviceModule> i_Modules, IInsDeviceRegistry i_Registry, ILogService i_LogService)
    {
        var registered = new HashSet<DeviceType>();

        foreach (IInsDeviceModule module in i_Modules)
        {
            if (!registered.Add(module.Type))
            { throw new InvalidOperationException($"Duplicate device module registration: {module.Type}"); }

            module.Register(i_Registry, i_LogService);
        }
    }
    #endregion
}