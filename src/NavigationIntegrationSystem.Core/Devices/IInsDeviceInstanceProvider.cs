using NavigationIntegrationSystem.Core.Enums;

namespace NavigationIntegrationSystem.Core.Devices;

// Provides stable, session-wide unique indices for device instances of the same type
public interface IInsDeviceInstanceProvider
{
    // Returns the session-stable ID for a specific device instance
    ushort GetInstanceId(IInsDevice i_Device);
}