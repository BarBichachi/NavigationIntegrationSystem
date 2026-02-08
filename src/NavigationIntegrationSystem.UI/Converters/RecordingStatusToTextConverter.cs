using Microsoft.UI.Xaml.Data;

using System;

namespace NavigationIntegrationSystem.UI.Converters;

public sealed partial class RecordingStatusToTextConverter : IValueConverter
{
    // Converts recording state to label
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value is bool isRecording && isRecording) ? "Stop Recording" : "Start Recording";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}