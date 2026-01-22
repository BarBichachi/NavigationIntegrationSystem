using System;

namespace NavigationIntegrationSystem.Core.Logging;

// Minimal logging contract used by non-UI layers
public interface ILogService
{
    event Action<LogRecord>? RecordWritten;

    void Info(string i_Source, string i_Message);
    void Debug(string i_Source, string i_Message);
    void Warn(string i_Source, string i_Message);
    void Error(string i_Source, string i_Message, Exception? i_Exception = null);
}