using System.Collections.Generic;

namespace NavigationIntegrationSystem.Core.Integration;

// Maps integration grid row names (IntegrationFieldNames.*) to the CSV column keys
// produced by PlaybackDeviceModule.BuildDefinition / consumed by CsvPlaybackSchema.
// Velocity Total is intentionally absent — it is a calculated row with no source.
public static class IntegrationFieldKeyMap
{
    #region Properties
    public static IReadOnlyDictionary<string, string> FieldToCsvKey { get; } = new Dictionary<string, string>
    {
        [IntegrationFieldNames.Latitude]      = "PositionLatValue",
        [IntegrationFieldNames.Longitude]     = "PositionLonValue",
        [IntegrationFieldNames.Altitude]      = "PositionAltValue",

        [IntegrationFieldNames.Roll]          = "EulerRollValue",
        [IntegrationFieldNames.Pitch]         = "EulerPitchValue",
        [IntegrationFieldNames.Azimuth]       = "EulerAzimuthValue",

        [IntegrationFieldNames.RollRate]      = "EulerRollRateValue",
        [IntegrationFieldNames.PitchRate]     = "EulerPitchRateValue",
        [IntegrationFieldNames.AzimuthRate]   = "EulerAzimuthRateValue",

        [IntegrationFieldNames.VelocityNorth] = "VelocityNorthValue",
        [IntegrationFieldNames.VelocityEast]  = "VelocityEastValue",
        [IntegrationFieldNames.VelocityDown]  = "VelocityDownValue",

        [IntegrationFieldNames.Course]        = "CourseValue",
    };
    #endregion
}
