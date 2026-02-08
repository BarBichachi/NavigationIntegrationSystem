using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Models.Devices;
using NavigationIntegrationSystem.Devices.Models;

using System;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Devices.Runtime;

// Dictionary-based registry for device creation and session-stable ID management
public sealed class InsDeviceRegistry : IInsDeviceRegistry, IInsDeviceInstanceProvider
{
    #region Private Fields
    private readonly Dictionary<DeviceType, Func<DeviceDefinition, DeviceConfig, IInsDevice>> m_Factories = new();
    private readonly Dictionary<DeviceType, ushort> m_TypeCounters = new();
    private readonly Dictionary<IInsDevice, ushort> m_AssignedIds = new();
    private readonly object m_Lock = new();
    #endregion

    #region Functions
    // Registers a device factory for a given device type
    public void Register(DeviceType i_Type, Func<DeviceDefinition, DeviceConfig, IInsDevice> i_Factory)
    {
        m_Factories[i_Type] = i_Factory;
    }

    // Creates a device instance and assigns it a stable session ID
    public IInsDevice Create(DeviceDefinition i_Definition, DeviceConfig i_Config)
    {
        if (!m_Factories.TryGetValue(i_Definition.Type, out var factory))
        {
            throw new NotSupportedException($"Unsupported device type: {i_Definition.Type}");
        }

        IInsDevice device = factory(i_Definition, i_Config);

        lock (m_Lock)
        {
            if (!m_TypeCounters.TryGetValue(i_Definition.Type, out ushort currentCount))
            {
                currentCount = 0;
            }

            m_AssignedIds[device] = currentCount;
            m_TypeCounters[i_Definition.Type] = (ushort)(currentCount + 1);
        }

        return device;
    }

    // Retrieves the assigned ID for an existing device instance
    public ushort GetInstanceId(IInsDevice i_Device)
    {
        lock (m_Lock)
        {
            return m_AssignedIds.TryGetValue(i_Device, out ushort id) ? id : (ushort)0;
        }
    }
    #endregion
}