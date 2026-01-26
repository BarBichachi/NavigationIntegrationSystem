using NavigationIntegrationSystem.Devices.Models;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Infrastructure.Persistence.DevicesConfig;

// Represents the root structure saved to devices.json
public sealed class DevicesConfigFile
{
    #region Properties
    public List<DeviceConfig> Devices { get; set; } = new List<DeviceConfig>();
    #endregion
}