using System.Collections.Generic;

namespace NavigationIntegrationSystem.Core.Integration;

// Maps integration grid row names (IntegrationFieldNames.*) to the per-device-type field
// keys that identify which scalar to pull out of a given device's telemetry payload.
//
// FieldToCsvKey: CSV column keys produced by PlaybackDeviceModule.BuildDefinition / consumed
// by CsvPlaybackSchema. Keys (EulerAzimuth*) are locked to the parent solution's recorder
// format (see TO_BE_DELETED/IntegratedInsOutputItem.IntegratedInsOutputColumns), even though
// NIS now labels these rows "Yaw" instead of "Azimuth".
//
// FieldToVn310Key: property names on Vn310Telemetry (lined up with Vn310DeviceModule's field
// definitions). Course is omitted because VN310 does not produce it directly.
//
// Velocity Total is intentionally absent from both — it is a calculated row with no source.
public static class IntegrationFieldKeyMap
{
    #region Properties
    public static IReadOnlyDictionary<string, string> FieldToCsvKey { get; } = new Dictionary<string, string>
    {
        [IntegrationFieldNames.Latitude]      = "PositionLatValue",
        [IntegrationFieldNames.Longitude]     = "PositionLonValue",
        [IntegrationFieldNames.Altitude]      = "PositionAltValue",

        [IntegrationFieldNames.Yaw]           = "EulerAzimuthValue",
        [IntegrationFieldNames.Pitch]         = "EulerPitchValue",
        [IntegrationFieldNames.Roll]          = "EulerRollValue",

        [IntegrationFieldNames.YawRate]       = "EulerAzimuthRateValue",
        [IntegrationFieldNames.PitchRate]     = "EulerPitchRateValue",
        [IntegrationFieldNames.RollRate]      = "EulerRollRateValue",

        [IntegrationFieldNames.VelocityNorth] = "VelocityNorthValue",
        [IntegrationFieldNames.VelocityEast]  = "VelocityEastValue",
        [IntegrationFieldNames.VelocityDown]  = "VelocityDownValue",

        [IntegrationFieldNames.Course]        = "CourseValue",
    };

    public static IReadOnlyDictionary<string, string> FieldToVn310Key { get; } = new Dictionary<string, string>
    {
        [IntegrationFieldNames.Latitude]      = "LatDeg",
        [IntegrationFieldNames.Longitude]     = "LonDeg",
        [IntegrationFieldNames.Altitude]      = "AltM",

        [IntegrationFieldNames.Yaw]           = "YawDeg",
        [IntegrationFieldNames.Pitch]         = "PitchDeg",
        [IntegrationFieldNames.Roll]          = "RollDeg",

        [IntegrationFieldNames.YawRate]       = "YawRateDegS",
        [IntegrationFieldNames.PitchRate]     = "PitchRateDegS",
        [IntegrationFieldNames.RollRate]      = "RollRateDegS",

        [IntegrationFieldNames.VelocityNorth] = "VelNorth",
        [IntegrationFieldNames.VelocityEast]  = "VelEast",
        [IntegrationFieldNames.VelocityDown]  = "VelDown",
    };
    #endregion
}
