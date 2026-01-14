using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Infrastructure.Configuration.Devices;
using NavigationIntegrationSystem.Infrastructure.Logging;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Services.Devices;

// VN310 module: definition + runtime registration
public sealed class Vn310DeviceModule : IInsDeviceModule
{
    #region Properties
    public DeviceType Type => DeviceType.VN310;
    #endregion

    #region Functions
    // Builds the VN310 device definition
    public DeviceDefinition BuildDefinition()
    {
        return new DeviceDefinition(
            i_Type: DeviceType.VN310,
            i_Fields: new List<DeviceFieldDefinition>
            {
                new DeviceFieldDefinition("UtcTime", "UTC Time", ""),
                new DeviceFieldDefinition("LatDeg", "Latitude", "deg"),
                new DeviceFieldDefinition("LonDeg", "Longitude", "deg"),
                new DeviceFieldDefinition("AltM", "Altitude", "m"),

                new DeviceFieldDefinition("YawDeg", "Yaw", "deg"),
                new DeviceFieldDefinition("PitchDeg", "Pitch", "deg"),
                new DeviceFieldDefinition("RollDeg", "Roll", "deg"),

                new DeviceFieldDefinition("YawRateDegS", "Yaw Rate", "deg/s"),
                new DeviceFieldDefinition("PitchRateDegS", "Pitch Rate", "deg/s"),
                new DeviceFieldDefinition("RollRateDegS", "Roll Rate", "deg/s"),

                new DeviceFieldDefinition("VelNorth", "Velocity North", "m/s"),
                new DeviceFieldDefinition("VelEast", "Velocity East", "m/s"),
                new DeviceFieldDefinition("VelDown", "Velocity Down", "m/s"),
                new DeviceFieldDefinition("Speed", "Speed", "m/s"),

                new DeviceFieldDefinition("AttUnc", "Att Uncertainty", ""),
                new DeviceFieldDefinition("PosUnc", "Pos Uncertainty", ""),
                new DeviceFieldDefinition("VelUnc", "Vel Uncertainty", ""),

                new DeviceFieldDefinition("InsStatus", "INS Status", ""),
                new DeviceFieldDefinition("TimeStatus", "Time Status", "")
            });
    }

    // Registers runtime creation for VN310
    public void Register(IInsDeviceRegistry i_Registry, LogService i_LogService)
    {
        i_Registry.Register(Type, (DeviceDefinition def, DeviceConfig cfg) => new Vn310InsDevice(def, cfg, i_LogService));
    }
    #endregion
}