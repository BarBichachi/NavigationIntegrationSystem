using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using NavigationIntegrationSystem.Core.Devices;
using System;

namespace NavigationIntegrationSystem.UI.Converters;

// Collapses a control when severity is Unknown -- used by DeviceModeChip so the chip disappears entirely when the device has no mode data (vs rendering an empty pill). Single-purpose; if a card-mode-aware control needs the same logic later, it can reuse this converter
public sealed class DeviceModeSeverityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DeviceModeSeverity severity) { return Visibility.Collapsed; }
        return severity == DeviceModeSeverity.Unknown ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
