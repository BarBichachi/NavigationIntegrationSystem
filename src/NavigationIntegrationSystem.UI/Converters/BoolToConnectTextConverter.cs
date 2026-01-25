using Microsoft.UI.Xaml.Data;
using System;

namespace NavigationIntegrationSystem.UI.Converters;

// Converts connection state boolean to Connect/Disconnect label
public sealed partial class BoolToConnectTextConverter : IValueConverter
{
    #region Functions
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isConnected = value is bool b && b;
        return isConnected ? "Disconnect" : "Connect";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) { return false; }
    #endregion
}