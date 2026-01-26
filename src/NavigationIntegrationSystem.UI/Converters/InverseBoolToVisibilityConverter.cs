using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace NavigationIntegrationSystem.UI.Converters;

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    // Returns Collapsed if true, Visible if false
    public object Convert(object i_Value, Type i_TargetType, object i_Parameter, string i_Language)
    {
        if (i_Value is bool value) return value ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object i_Value, Type i_TargetType, object i_Parameter, string i_Language) => throw new NotImplementedException();
}