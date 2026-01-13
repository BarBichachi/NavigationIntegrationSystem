using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace NavigationIntegrationSystem.UI.Converters;

// Converts null or empty string to Collapsed, otherwise Visible
public sealed class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
