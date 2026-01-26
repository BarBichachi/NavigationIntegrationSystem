using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Devices.Config;
using NavigationIntegrationSystem.Devices.Implementations;
using NavigationIntegrationSystem.Devices.Runtime;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Devices.Modules;

// TMaps100X module: definition + runtime registration
public sealed class Tmaps100XDeviceModule : IInsDeviceModule
{
    #region Properties
    public DeviceType Type => DeviceType.Tmaps100X;
    #endregion

    #region Functions
    // Builds the TMaps100X device definition
    public DeviceDefinition BuildDefinition()
    {
        return new DeviceDefinition(
            i_Type: DeviceType.Tmaps100X,
            i_Fields: new List<DeviceFieldDefinition>
            {
                new("UtcTime", "UTC Time", ""),
                new("LatDeg", "Latitude", "deg"),
                new("LonDeg", "Longitude", "deg"),
                new("AltM", "Altitude", "m"),

                new("AzimuthDeg", "Azimuth", "deg"),
                new DeviceFieldDefinition("PitchDeg", "Pitch", "deg"),
                new DeviceFieldDefinition("RollDeg", "Roll", "deg"),

                new DeviceFieldDefinition("AzimuthRateDegS", "Azimuth Rate", "deg/s"),
                new DeviceFieldDefinition("PitchRateDegS", "Pitch Rate", "deg/s"),
                new DeviceFieldDefinition("RollRateDegS", "Roll Rate", "deg/s"),

                new DeviceFieldDefinition("VelNorth", "Velocity North", "m/s"),
                new DeviceFieldDefinition("VelEast", "Velocity East", "m/s"),
                new DeviceFieldDefinition("VelDown", "Velocity Down", "m/s"),
                new DeviceFieldDefinition("Speed", "Speed", "m/s"),

                new DeviceFieldDefinition("AlignmentState", "Alignment State", ""),
                new DeviceFieldDefinition("GpsStatus", "GPS Status", ""),
                new DeviceFieldDefinition("GeneralStatus", "General Status", "")
            });
    }

    // Registers runtime creation for TMaps100X
    public void Register(IInsDeviceRegistry i_Registry, ILogService i_LogService)
    {
        i_Registry.Register(Type, (DeviceDefinition def, DeviceConfig cfg) => new Tmaps100XInsDevice(def, cfg, i_LogService));
    }
    #endregion
}
