using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;

namespace NavigationIntegrationSystem.Infrastructure.Logging;

// Writes logs to file and stores a bounded in-memory buffer for UI viewing
public sealed class LogService
{
    #region Private Fields
    private readonly SemaphoreSlim m_FileLock = new SemaphoreSlim(1, 1);
    private readonly string m_LogFolderPath;
    private readonly int m_MaxUiEntries;
    private readonly DispatcherQueue? m_DispatcherQueue;
    #endregion

    #region Properties
    public ObservableCollection<LogEntry> Entries { get; } = new ObservableCollection<LogEntry>();
    #endregion

    #region Ctors
    public LogService(string i_LogFolderPath, int i_MaxUiEntries, DispatcherQueue? i_DispatcherQueue)
    {
        m_LogFolderPath = i_LogFolderPath;
        m_MaxUiEntries = i_MaxUiEntries <= 0 ? 2000 : i_MaxUiEntries;
        m_DispatcherQueue = i_DispatcherQueue;

        Directory.CreateDirectory(m_LogFolderPath);
    }
    #endregion

    #region Functions
    // Logs an informational message to file and in-memory buffer
    public void Info(string i_Category, string i_Message) { Append("INFO", i_Category, i_Message, null); }

    // Logs an error message to file and in-memory buffer
    public void Error(string i_Category, string i_Message, Exception? i_Exception = null) { Append("ERROR", i_Category, i_Message, i_Exception); }

    // Appends a log entry and persists it to disk
    private void Append(string i_Level, string i_Category, string i_Message, Exception? i_Exception)
    {
        string message = i_Exception == null ? i_Message : $"{i_Message}{Environment.NewLine}{i_Exception}";
        var entry = new LogEntry(DateTime.Now, i_Level, i_Category, message);

        AddEntryToUi(entry);
        _ = WriteToFileAsync(entry);
    }

    // Adds the entry to the UI buffer on the UI thread
    private void AddEntryToUi(LogEntry i_Entry)
    {
        if (m_DispatcherQueue == null || m_DispatcherQueue.HasThreadAccess)
        {
            AddEntryToCollection(i_Entry);
            return;
        }

        m_DispatcherQueue.TryEnqueue(() => { AddEntryToCollection(i_Entry); });
    }

    // Adds the entry to the observable collection and enforces the bounded size
    private void AddEntryToCollection(LogEntry i_Entry)
    {
        Entries.Add(i_Entry);
        if (Entries.Count > m_MaxUiEntries) { Entries.RemoveAt(0); }
    }

    // Writes the entry to the current daily log file
    private async Task WriteToFileAsync(LogEntry i_Entry)
    {
        string filePath = Path.Combine(m_LogFolderPath, $"{DateTime.Now:yyyy-MM-dd}.log");
        string line = $"{i_Entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{i_Entry.Level}] {i_Entry.Category} - {i_Entry.Message}{Environment.NewLine}";

        await m_FileLock.WaitAsync().ConfigureAwait(false);
        try
        { await File.AppendAllTextAsync(filePath, line, Encoding.UTF8).ConfigureAwait(false); }
        finally { m_FileLock.Release(); }
    }
    #endregion
}