using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

using System;

namespace NavigationIntegrationSystem.UI.Converters;

public sealed partial class RecordingStatusToIconConverter : IValueConverter
{
    // Converts recording state to symbol icon
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value is bool isRecording && isRecording) ? Symbol.Stop : Symbol.Video;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}