
using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace NavigationIntegrationSystem.UI.Converters;

// Converts boolean to Visibility
public sealed class BoolToVisibilityConverter : IValueConverter
{
    #region Functions
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isVisible = value is bool b && b;
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility visibility && visibility == Visibility.Visible;
    }
    #endregion
}