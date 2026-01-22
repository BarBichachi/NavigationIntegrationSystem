using System.Collections.ObjectModel;

using Microsoft.UI.Dispatching;

using NavigationIntegrationSystem.Core.Logging;

namespace NavigationIntegrationSystem.UI.Services.Logging;

// Maintains a bounded UI log buffer and marshals updates to the UI thread
public sealed class UiLogBuffer
{
    #region Private Fields
    private readonly ILogService m_LogService;
    private readonly int m_MaxEntries;
    private DispatcherQueue? m_DispatcherQueue;
    #endregion

    #region Properties
    public ObservableCollection<UiLogEntry> Entries { get; } = new ObservableCollection<UiLogEntry>();
    #endregion

    #region Ctors
    public UiLogBuffer(ILogService i_LogService, int i_MaxEntries)
    {
        m_LogService = i_LogService;
        m_MaxEntries = i_MaxEntries <= 0 ? 2000 : i_MaxEntries;

        m_LogService.RecordWritten += OnRecordWritten;
    }
    #endregion

    #region Functions
    // Attaches dispatcher for UI-thread marshaling
    public void AttachUiDispatcher(DispatcherQueue i_DispatcherQueue)
    {
        if (m_DispatcherQueue != null) { return; }
        m_DispatcherQueue = i_DispatcherQueue;
    }

    // Clears the UI log buffer
    public void Clear()
    {
        if (m_DispatcherQueue == null || m_DispatcherQueue.HasThreadAccess) { Entries.Clear(); return; }
        m_DispatcherQueue.TryEnqueue(() => { Entries.Clear(); });
    }

    // Removes a single entry from the buffer
    public void Remove(UiLogEntry i_Entry)
    {
        if (m_DispatcherQueue == null || m_DispatcherQueue.HasThreadAccess) { Entries.Remove(i_Entry); return; }
        m_DispatcherQueue.TryEnqueue(() => { Entries.Remove(i_Entry); });
    }

    // Handles incoming log records from the logger
    private void OnRecordWritten(LogRecord i_Record)
    {
        var entry = new UiLogEntry(i_Record);

        if (m_DispatcherQueue == null || m_DispatcherQueue.HasThreadAccess) { Add(entry); return; }
        m_DispatcherQueue.TryEnqueue(() => { Add(entry); });
    }

    // Adds an entry and enforces bounded size
    private void Add(UiLogEntry i_Entry)
    {
        Entries.Add(i_Entry);
        if (Entries.Count > m_MaxEntries) { Entries.RemoveAt(0); }
    }
    #endregion
}