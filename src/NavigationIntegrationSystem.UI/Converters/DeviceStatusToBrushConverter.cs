using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NavigationIntegrationSystem.Core.Enums;
using System;
using Windows.UI;

namespace NavigationIntegrationSystem.UI.Converters;

// Converts DeviceStatus to a foreground brush for UI status display
public sealed class DeviceStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DeviceStatus status)
        {
            return new SolidColorBrush(Color.FromArgb(255, 128, 128, 128)); // fallback gray
        }

        return status switch
        {
            DeviceStatus.Connected => new SolidColorBrush(Color.FromArgb(255, 16, 124, 16)), // green

            DeviceStatus.Connecting => new SolidColorBrush(Color.FromArgb(255, 0, 120, 212)), // blue

            DeviceStatus.Error => new SolidColorBrush(Color.FromArgb(255, 196, 43, 28)), // red

            _ => new SolidColorBrush(Color.FromArgb(255, 128, 128, 128)) // disconnected / unknown
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}