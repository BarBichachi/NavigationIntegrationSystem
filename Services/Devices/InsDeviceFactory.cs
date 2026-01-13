using System;

using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;
using NavigationIntegrationSystem.Infrastructure.Logging;

namespace NavigationIntegrationSystem.Services.Devices;

// Builds per-device runtime instances
public sealed class InsDeviceFactory : IInsDeviceFactory
{
    #region Private Fields
    private readonly LogService m_LogService;
    #endregion

    #region Ctors
    public InsDeviceFactory(LogService i_LogService)
    {
        m_LogService = i_LogService;
    }
    #endregion

    #region Functions
    // Creates a runtime device instance for a catalog definition
    public IInsDevice Create(DeviceDefinition i_Definition, DeviceConfig i_Config)
    {
        return i_Definition.Type switch
        {
            DeviceType.VN310 => new Vn310InsDevice(i_Definition, m_LogService),
            DeviceType.Tmaps100X => new Tmaps100XInsDevice(i_Definition, m_LogService),
            _ => throw new NotSupportedException($"Unsupported device type: {i_Definition.Type}")
        };
    }
    #endregion
}