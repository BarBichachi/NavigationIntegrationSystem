using System;

using NavigationIntegrationSystem.Core.Logging;

namespace NavigationIntegrationSystem.UI.Services.Logging;

// Represents a single log row displayed in the UI
public sealed class UiLogEntry
{
    #region Properties
    public DateTime TimestampLocal { get; }
    public string TimestampText => TimestampLocal.ToString("HH:mm:ss");
    public string Level { get; }
    public string Source { get; }
    public string Message { get; }
    #endregion

    #region Ctors
    public UiLogEntry(LogRecord i_Record)
    {
        TimestampLocal = i_Record.TimestampUtc.ToLocalTime();
        Level = i_Record.Level.ToString();
        Source = i_Record.Source;
        Message = string.IsNullOrWhiteSpace(i_Record.ExceptionText) ? i_Record.Message : $"{i_Record.Message}{Environment.NewLine}{i_Record.ExceptionText}";
    }
    #endregion
}