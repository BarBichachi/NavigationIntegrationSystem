using System.Collections.Generic;
using System.Linq;

using NavigationIntegrationSystem.Core.Models;

namespace NavigationIntegrationSystem.Services.Devices;

// Provides the fixed list of device instances and their inspectable field definitions
public sealed class DeviceCatalogService
{
    #region Private Fields
    private readonly IEnumerable<IInsDeviceModule> m_Modules;
    #endregion

    #region Ctors
    public DeviceCatalogService(IEnumerable<IInsDeviceModule> i_Modules)
    {
        m_Modules = i_Modules;
    }
    #endregion

    #region Functions
    // Returns the fixed device definitions used by the application (the order is by device type for consistency)
    public IReadOnlyList<DeviceDefinition> GetDevices()
    {
        return m_Modules.OrderBy(m => m.Type).Select(m => m.BuildDefinition()).ToList();
    }
    #endregion
}