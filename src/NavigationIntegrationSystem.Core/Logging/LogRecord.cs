using System;

namespace NavigationIntegrationSystem.Core.Logging;

// Immutable log record produced by the system
public sealed class LogRecord
{
    #region Properties
    public DateTime TimestampUtc { get; }
    public LogLevel Level { get; }
    public string Source { get; }
    public string Message { get; }
    public string? ExceptionText { get; }
    #endregion

    #region Ctors
    public LogRecord(DateTime i_TimestampUtc, LogLevel i_Level, string i_Source, string i_Message, string? i_ExceptionText)
    {
        TimestampUtc = i_TimestampUtc;
        Level = i_Level;
        Source = i_Source;
        Message = i_Message;
        ExceptionText = i_ExceptionText;
    }
    #endregion
}
