using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using NavigationIntegrationSystem.UI.Enums;
using System;

namespace NavigationIntegrationSystem.UI.Converters;

// Visible only when pane mode is Inspect
public sealed class PaneModeToInspectVisibilityConverter : IValueConverter
{
    #region Functions
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is DevicesPaneMode mode && mode == DevicesPaneMode.Inspect ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) { return DevicesPaneMode.None; }
    #endregion
}
