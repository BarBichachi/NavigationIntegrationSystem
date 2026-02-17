using System;
using System.Linq;

namespace NavigationIntegrationSystem.Devices.Validation;

// Defines the expected CSV schema for playback files
public static class CsvPlaybackSchema
{
    #region Properties
    public static string[] Columns { get; } =
    {
        "PositionLatValue", "PositionLonValue", "PositionAltValue",
        "EulerRollValue", "EulerPitchValue", "EulerAzimuthValue",
        "EulerRollRateValue", "EulerPitchRateValue", "EulerAzimuthRateValue",
        "VelocityNorthValue", "VelocityEastValue", "VelocityDownValue", "VelocityTotalValue",
        "CourseValue", "StatusValue"
    };
    #endregion

    #region Functions
    // Splits and trims a CSV line into cells
    public static string[] ParseCsvLine(string i_Line)
    {
        if (string.IsNullOrEmpty(i_Line)) { return Array.Empty<string>(); }
        return i_Line.Split(',').Select(s => s.Trim()).ToArray();
    }
    #endregion
}
