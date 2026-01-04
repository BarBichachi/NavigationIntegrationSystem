using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

using NavigationIntegrationSystem.UI.ViewModels.Devices;

namespace NavigationIntegrationSystem.UI.Converters;

// Visible only when pane mode is Settings
public sealed class PaneModeToSettingsVisibilityConverter : IValueConverter
{
    #region Functions
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is DevicesPaneMode mode && mode == DevicesPaneMode.Settings ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) { return DevicesPaneMode.None; }
    #endregion
}