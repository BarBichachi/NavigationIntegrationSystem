using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using NavigationIntegrationSystem.Core.Logging;

namespace NavigationIntegrationSystem.Infrastructure.Logging;

// Writes logs to file via a bounded background channel; broadcasts log records to listeners synchronously.
public sealed class FileLogService : ILogService, ILogPaths, IDisposable
{
    #region Constants
    // 10k records ≈ ~1MB at typical line lengths - enough headroom for a brief disk hiccup, small enough not to balloon memory under sustained backpressure.
    private const int c_ChannelCapacity = 10_000;
    // Bounded wait when stopping the consumer so a stuck disk can't hang app shutdown forever.
    private const int c_ShutdownDrainTimeoutMs = 2000;
    #endregion

    #region Private Fields
    private readonly string m_LogFolderPath;
    private readonly Channel<LogRecord> m_Channel;
    private readonly Task m_ConsumerTask;
    private long m_DroppedRecordCount;
    private bool m_Disposed;
    #endregion

    #region Properties
    public string LogFolderPath => m_LogFolderPath;

    // Number of records dropped because the channel was at capacity (sustained disk slowness). Exposed for diagnostics.
    public long DroppedRecordCount => Interlocked.Read(ref m_DroppedRecordCount);
    #endregion

    #region Events
    // Fires synchronously on the calling thread for every log record. Used by the in-memory UI log buffer.
    public event Action<LogRecord>? RecordWritten;

    // Dead-letter event: fires from the consumer task when a file write throws. Subscribers should treat this as a non-fatal observability signal - disk full, permission denied, etc. If unsubscribed, the failure is silently swallowed.
    public event Action<LogRecord, Exception>? WriteFailed;
    #endregion

    #region Ctors
    public FileLogService(string i_LogFolderPath)
    {
        m_LogFolderPath = i_LogFolderPath;
        Directory.CreateDirectory(m_LogFolderPath);

        // Wait mode + non-blocking TryWrite: when the channel is full we count the drop and move on, never blocking the caller (UI thread, recording loop, etc.).
        BoundedChannelOptions opts = new BoundedChannelOptions(c_ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        };
        m_Channel = Channel.CreateBounded<LogRecord>(opts);

        m_ConsumerTask = Task.Run(ConsumeAsync);
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

    // Creates a record, fires RecordWritten synchronously, then enqueues for the background writer.
    private void Write(LogLevel i_Level, string i_Source, string i_Message, string? i_ExceptionText)
    {
        LogRecord record = new LogRecord(DateTime.UtcNow, i_Level, i_Source, i_Message, i_ExceptionText);

        RecordWritten?.Invoke(record);

        if (!m_Channel.Writer.TryWrite(record))
        {
            Interlocked.Increment(ref m_DroppedRecordCount);
        }
    }

    // Background consumer: drains the channel one record at a time and persists each. Per-record try/catch ensures one disk error doesn't kill the loop; failures fire WriteFailed.
    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (LogRecord record in m_Channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    await WriteToFileAsync(record).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    WriteFailed?.Invoke(record, ex);
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    // Writes the record to the current daily log file. Single-reader channel guarantees no concurrent writers, so no lock is needed.
    private async Task WriteToFileAsync(LogRecord i_Record)
    {
        string filePath = Path.Combine(m_LogFolderPath, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
        string level = i_Record.Level.ToString().ToUpperInvariant();

        string line = $"{i_Record.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}Z [{level}] {i_Record.Source} - {i_Record.Message}";
        if (!string.IsNullOrWhiteSpace(i_Record.ExceptionText)) { line = $"{line}{Environment.NewLine}{i_Record.ExceptionText}"; }
        line = $"{line}{Environment.NewLine}";

        await File.AppendAllTextAsync(filePath, line, Encoding.UTF8).ConfigureAwait(false);
    }

    // On Dispose: signal no more writes, give the consumer up to 2s to drain pending records, then move on.
    public void Dispose()
    {
        if (m_Disposed) return;
        m_Disposed = true;

        m_Channel.Writer.TryComplete();
        try
        {
            m_ConsumerTask.Wait(TimeSpan.FromMilliseconds(c_ShutdownDrainTimeoutMs));
        }
        catch (AggregateException) { /* drain best-effort during shutdown */ }
    }
    #endregion
}
