using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NavigationIntegrationSystem.Core.Devices;
using System;
using Windows.UI;

namespace NavigationIntegrationSystem.UI.Converters;

// Maps a DeviceModeSeverity to a chip fill brush. Color palette tuned to match the existing DeviceStatus convention (green=Connected/Good, red=Error/Bad) so cards read consistently. Unknown returns a neutral gray; the chip control collapses on Unknown so this branch is mostly defensive
public sealed class DeviceModeSeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DeviceModeSeverity severity)
        {
            return new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
        }

        return severity switch
        {
            DeviceModeSeverity.Good => new SolidColorBrush(Color.FromArgb(255, 16, 124, 16)),     // green (matches DeviceStatus.Connected)
            DeviceModeSeverity.Warning => new SolidColorBrush(Color.FromArgb(255, 202, 138, 4)),  // amber
            DeviceModeSeverity.Caution => new SolidColorBrush(Color.FromArgb(255, 217, 119, 6)),  // orange
            DeviceModeSeverity.Bad => new SolidColorBrush(Color.FromArgb(255, 196, 43, 28)),      // red (matches DeviceStatus.Error)
            _ => new SolidColorBrush(Color.FromArgb(255, 128, 128, 128))                          // unknown -> gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
