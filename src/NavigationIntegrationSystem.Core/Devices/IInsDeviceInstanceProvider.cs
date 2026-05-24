using NavigationIntegrationSystem.Core.Enums;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Core.Devices;

// Provides stable, session-wide unique indices for device instances of the same type, plus an enumeration of all created instances (used by app-shutdown cleanup to disconnect still-active devices before the host disposes)
public interface IInsDeviceInstanceProvider
{
    // Returns the session-stable ID for a specific device instance. Throws InvalidOperationException if the device was never registered.
    ushort GetInstanceId(IInsDevice i_Device);

    // Snapshot of all devices created via the registry this session. Returned as a fresh enumerable so the caller can iterate without worrying about concurrent modification of the underlying store
    IReadOnlyList<IInsDevice> GetAllDevices();
}