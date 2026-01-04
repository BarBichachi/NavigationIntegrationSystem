using System;

using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

namespace NavigationIntegrationSystem.UI.Converters;

// Converts IsConnected to a green/red foreground brush
public sealed class BoolToStatusForegroundBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isConnected = value is bool b && b;

        return isConnected
            ? new SolidColorBrush(Color.FromArgb(255, 16, 124, 16))   // green
            : new SolidColorBrush(Color.FromArgb(255, 196, 43, 28));  // red
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) { throw new NotSupportedException(); }
}
