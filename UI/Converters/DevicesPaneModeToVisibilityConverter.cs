using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

using NavigationIntegrationSystem.UI.ViewModels.Devices;
namespace NavigationIntegrationSystem.UI.Converters;

// Converts DevicesPaneMode to Visibility for a specific pane type
public sealed class DevicesPaneModeToVisibilityConverter : IValueConverter
{
    #region Properties 
    public DevicesPaneMode TargetMode { get; set; } = DevicesPaneMode.None;
    #endregion

    #region Functions 
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DevicesPaneMode mode && mode == TargetMode)
        { return Visibility.Visible; }
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DevicesPaneMode.None;
    }
    #endregion
}