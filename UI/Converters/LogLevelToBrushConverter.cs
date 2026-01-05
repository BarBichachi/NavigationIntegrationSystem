using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace NavigationIntegrationSystem.UI.Converters;

// Converts log level string to a static badge brush
public sealed class LogLevelToBrushConverter : IValueConverter
{
    // Converts log level to brush
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string level = value as string ?? string.Empty;

        return level.ToUpperInvariant() switch
        {
            "DEBUG" => new SolidColorBrush(Colors.Gray),
            "INFO" => new SolidColorBrush(ColorHelper.FromArgb(255, 45, 125, 255)),   // #2D7DFF
            "WARN" => new SolidColorBrush(ColorHelper.FromArgb(255, 244, 162, 97)),   // #F4A261
            "ERROR" => new SolidColorBrush(ColorHelper.FromArgb(255, 237, 85, 100)),  // #ED5564
            _ => new SolidColorBrush(Colors.DarkGray)
        };
    }

    // Not used
    public object ConvertBack(object value, Type targetType, object parameter, string language) { return value; }
}
