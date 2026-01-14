using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;
using System;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Services.Devices;

// Dictionary-based registry for device creation
public sealed class InsDeviceRegistry : IInsDeviceRegistry
{
    #region Private Fields
    private readonly Dictionary<DeviceType, Func<DeviceDefinition, DeviceConfig, IInsDevice>> m_Factories = new();
    #endregion

    #region Functions
    // Registers a device factory for a given device type
    public void Register(DeviceType i_Type, Func<DeviceDefinition, DeviceConfig, IInsDevice> i_Factory)
    {
        m_Factories[i_Type] = i_Factory;
    }

    // Creates a device instance for a given definition/config
    public IInsDevice Create(DeviceDefinition i_Definition, DeviceConfig i_Config)
    {
        if (!m_Factories.TryGetValue(i_Definition.Type, out var factory))
        { throw new NotSupportedException($"Unsupported device type: {i_Definition.Type}"); }

        return factory(i_Definition, i_Config);
    }
    #endregion
}