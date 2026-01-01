using System.Collections.Generic;

using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Models;

namespace NavigationIntegrationSystem.Services.Devices;

// Provides the fixed list of device instances and their inspectable field definitions
public sealed class DeviceCatalogService
{
    #region Functions
    // Returns the fixed device definitions used by the application
    public IReadOnlyList<DeviceDefinition> GetDevices()
    {
        return new List<DeviceDefinition>
        {
            BuildVn310(),
            BuildTmaps100X()
        };
    }

    private DeviceDefinition BuildVn310()
    {
        return new DeviceDefinition(
            i_DeviceId: "VN310_1",
            i_DisplayName: "VN310",
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

    private DeviceDefinition BuildTmaps100X()
    {
        return new DeviceDefinition(
            i_DeviceId: "TMAPS100X_1",
            i_DisplayName: "Tmaps100X",
            i_Type: DeviceType.Tmaps100X,
            i_Fields: new List<DeviceFieldDefinition>
            {
                new DeviceFieldDefinition("UtcTime", "UTC Time", ""),
                new DeviceFieldDefinition("LatDeg", "Latitude", "deg"),
                new DeviceFieldDefinition("LonDeg", "Longitude", "deg"),
                new DeviceFieldDefinition("AltM", "Altitude", "m"),

                new DeviceFieldDefinition("AzimuthDeg", "Azimuth", "deg"),
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
    #endregion
}
