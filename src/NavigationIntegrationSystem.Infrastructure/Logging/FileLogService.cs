using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NavigationIntegrationSystem.Core.Logging;

namespace NavigationIntegrationSystem.Infrastructure.Logging;

// Writes logs to file and broadcasts log records to listeners
public sealed class FileLogService : ILogService, ILogPaths
{
    #region Private Fields
    private readonly SemaphoreSlim m_FileLock = new SemaphoreSlim(1, 1);
    private readonly string m_LogFolderPath;
    #endregion

    #region Properties
    public string LogFolderPath => m_LogFolderPath;
    #endregion

    #region Events
    public event Action<LogRecord>? RecordWritten;
    #endregion

    #region Ctors
    public FileLogService(string i_LogFolderPath)
    {
        m_LogFolderPath = i_LogFolderPath;
        Directory.CreateDirectory(m_LogFolderPath);
    }
    #endregion

    #region Functions
    // Logs a debug message
    public void Debug(string i_Source, string i_Message) { Write(LogLevel.Debug, i_Source, i_Message, null); }

    // Logs an informational message
    public void Info(string i_Source, string i_Message) { Write(LogLevel.Info, i_Source, i_Message, null); }

    // Logs a warning message
    public void Warn(string i_Source, string i_Message) { Write(LogLevel.Warn, i_Source, i_Message, null); }

    // Logs an error message
    public void Error(string i_Source, string i_Message, Exception? i_Exception = null)
    {
        Write(LogLevel.Error, i_Source, i_Message, i_Exception?.ToString());
    }

    // Creates a record, broadcasts it, and persists to disk
    private void Write(LogLevel i_Level, string i_Source, string i_Message, string? i_ExceptionText)
    {
        var record = new LogRecord(DateTime.UtcNow, i_Level, i_Source, i_Message, i_ExceptionText);

        RecordWritten?.Invoke(record);
        _ = WriteToFileAsync(record);
    }

    // Writes the record to the current daily log file
    private async Task WriteToFileAsync(LogRecord i_Record)
    {
        string filePath = Path.Combine(m_LogFolderPath, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
        string level = i_Record.Level.ToString().ToUpperInvariant();

        string line = $"{i_Record.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}Z [{level}] {i_Record.Source} - {i_Record.Message}";
        if (!string.IsNullOrWhiteSpace(i_Record.ExceptionText)) { line = $"{line}{Environment.NewLine}{i_Record.ExceptionText}"; }
        line = $"{line}{Environment.NewLine}";

        await m_FileLock.WaitAsync().ConfigureAwait(false);
        try { await File.AppendAllTextAsync(filePath, line, Encoding.UTF8).ConfigureAwait(false); }
        finally { m_FileLock.Release(); }
    }
    #endregion
}