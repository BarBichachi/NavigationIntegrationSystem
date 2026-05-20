using System.Collections.Generic;

namespace NavigationIntegrationSystem.Core.Integration;

// Maps integration grid row names (IntegrationFieldNames.*) to the CSV column keys
// produced by PlaybackDeviceModule.BuildDefinition / consumed by CsvPlaybackSchema.
// The CSV keys (EulerAzimuth*) are locked to the parent solution's recorder format
// (see TO_BE_DELETED/IntegratedInsOutputItem.IntegratedInsOutputColumns), even though
// NIS now labels these rows "Yaw" instead of "Azimuth". Velocity Total is intentionally
// absent — it is a calculated row with no source.
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
    #endregion
}
