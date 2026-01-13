using System;

namespace NavigationIntegrationSystem.Infrastructure.Logging;

// Represents a single log record shown in the UI log list
public sealed class LogEntry
{
    public DateTime Timestamp { get; }
    public string Level { get; }
    public string Category { get; }
    public string Message { get; }
    public string TimestampText => Timestamp.ToString("HH:mm:ss");

    public LogEntry(DateTime i_Timestamp, string i_Level, string i_Category, string i_Message)
    {
        Timestamp = i_Timestamp;
        Level = i_Level;
        Category = i_Category;
        Message = i_Message;
    }
}

