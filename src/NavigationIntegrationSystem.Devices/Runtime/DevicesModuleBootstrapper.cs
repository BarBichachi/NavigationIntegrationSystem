// ---------------------------------------------------------
// FILE: .\src\NavigationIntegrationSystem.Devices\Runtime\DevicesModuleBootstrapper.cs
// ---------------------------------------------------------

using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Devices.Modules;
using System;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Devices.Runtime;

// Bootstraps device modules into the device registry
public sealed class DevicesModuleBootstrapper
{
    #region Private Fields
    private readonly IEnumerable<IInsDeviceModule> m_Modules;
    private readonly IInsDeviceRegistry m_Registry;
    private readonly ILogService m_LogService;
    #endregion

    #region Ctors
    public DevicesModuleBootstrapper(IEnumerable<IInsDeviceModule> i_Modules, IInsDeviceRegistry i_Registry, ILogService i_LogService)
    {
        m_Modules = i_Modules;
        m_Registry = i_Registry;
        m_LogService = i_LogService;
    }
    #endregion

    #region Functions
    // Registers all device modules into the registry. Must be called before UI initialization.
    public void Initialize()
    {
        var registered = new HashSet<DeviceType>();

        foreach (IInsDeviceModule module in m_Modules)
        {
            if (!registered.Add(module.Type))
            {
                throw new InvalidOperationException($"Duplicate device module registration: {module.Type}");
            }

            module.Register(m_Registry, m_LogService);
        }

        m_LogService.Info(nameof(DevicesModuleBootstrapper), "Device modules registered.");
    }
    #endregion
}