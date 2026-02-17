using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace NavigationIntegrationSystem.UI.Converters;

// Converts between selected items and int values while ignoring null selections
public sealed class SelectionToIntConverter : IValueConverter
{
    // Converts an int value to a selected item value
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null) { return parameter ?? 0; }
        return value;
    }

    // Converts a selected item back to an int value, ignoring null selections
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value == null) { return DependencyProperty.UnsetValue; }
        if (value is int intValue) { return intValue; }
        if (int.TryParse(value.ToString(), out int parsed)) { return parsed; }
        return DependencyProperty.UnsetValue;
    }
}
