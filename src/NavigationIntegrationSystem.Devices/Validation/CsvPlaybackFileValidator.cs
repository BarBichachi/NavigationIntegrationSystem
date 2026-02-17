using System.IO;
using System.Linq;

namespace NavigationIntegrationSystem.Devices.Validation;

// Validates playback CSV files against the expected schema
public static class CsvPlaybackFileValidator
{
    #region Functions
    // Validates the playback CSV file and returns an error message when invalid
    public static bool ValidateFile(string i_FilePath, out string o_ErrorMessage)
    {
        if (string.IsNullOrWhiteSpace(i_FilePath))
        {
            o_ErrorMessage = "Playback file path is required.";
            return false;
        }

        if (!File.Exists(i_FilePath))
        {
            o_ErrorMessage = "Playback file not found.";
            return false;
        }

        if (!string.Equals(Path.GetExtension(i_FilePath), ".csv", System.StringComparison.OrdinalIgnoreCase))
        {
            o_ErrorMessage = "Playback file must be a .csv file.";
            return false;
        }

        string[] lines = File.ReadAllLines(i_FilePath);
        if (lines.Length < 2)
        {
            o_ErrorMessage = "CSV file must contain a header row and at least one data row.";
            return false;
        }

        string[] headers = CsvPlaybackSchema.ParseCsvLine(lines[0]);
        if (headers.Length == 0)
        {
            o_ErrorMessage = "CSV header row is missing.";
            return false;
        }

        if (!CsvPlaybackSchema.Columns.SequenceEqual(headers))
        {
            o_ErrorMessage = "CSV header does not match the expected schema.";
            return false;
        }

        o_ErrorMessage = string.Empty;
        return true;
    }
    #endregion
}
