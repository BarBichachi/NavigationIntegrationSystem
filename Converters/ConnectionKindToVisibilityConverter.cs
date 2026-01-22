using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using NavigationIntegrationSystem.Devices.Config.Enums;
using System;

namespace NavigationIntegrationSystem.UI.Converters;

// Converts DeviceConnectionKind to Visibility using ConverterParameter (Udp/Tcp/Serial)
public sealed class ConnectionKindToVisibilityConverter : IValueConverter
{
    #region Functions
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DeviceConnectionKind kind) { return Visibility.Collapsed; }
        if (parameter is not string targetKindText)  { return Visibility.Collapsed; }
        if (!Enum.TryParse(targetKindText, out DeviceConnectionKind targetKind)) { return Visibility.Collapsed; }

        return kind == targetKind ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) { throw new NotSupportedException(); }
    #endregion
}