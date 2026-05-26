using System.Collections.Generic;
using System.Collections.ObjectModel;

using Microsoft.UI.Dispatching;

using NavigationIntegrationSystem.Core.Logging;

namespace NavigationIntegrationSystem.UI.Services.Logging;

// Maintains a bounded UI log buffer and marshals updates to the UI thread. ObservableCollection<T> is not thread-safe and is data-bound to WinUI controls, so every mutation of Entries MUST happen on the UI thread -- otherwise WinUI's binding system reads a half-mutated internal array and crashes with AccessViolationException. Three windows need to be sealed: (1) the pre-attach gap between this object's construction and AttachUiDispatcher (entries queued under m_PreAttachLock and flushed via dispatcher on attach); (2) cross-thread direct mutation after attach (always go through TryEnqueue); (3) shutdown -- between the moment the user closes the app and host StopAsync completes, hosted services log a flurry of "Disconnecting ..." messages from worker threads; if any of those Add callbacks land on the UI thread AFTER WinUI has begun tearing down the data-bound view tree, the CollectionChanged event fires into freed native subscribers and AVEs. BeginShutdown() seals window (3) by setting m_AcceptingRecords=false (a volatile flag checked both at OnRecordWritten entry AND inside every dispatched callback) and unsubscribing from RecordWritten
public sealed class UiLogBuffer
{
    #region Private Fields
    private readonly ILogService m_LogService;
    private readonly int m_MaxEntries;
    private DispatcherQueue? m_DispatcherQueue;

    // Pre-attach queue + lock. Mutated only while holding m_PreAttachLock so writes from concurrent worker threads (RecordWritten is sync on the caller's thread) can't race. Null after AttachUiDispatcher has flushed it
    private readonly object m_PreAttachLock = new();
    private List<UiLogEntry>? m_PreAttachBuffer = new List<UiLogEntry>();

    // Master enable flag. False after BeginShutdown -- both new OnRecordWritten invocations AND already-queued dispatcher callbacks check this so nothing mutates Entries past the shutdown boundary
    private volatile bool m_AcceptingRecords = true;
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
    // Attaches the UI dispatcher and flushes any pre-attach entries onto the UI thread in arrival order. Idempotent: subsequent calls are no-ops
    public void AttachUiDispatcher(DispatcherQueue i_DispatcherQueue)
    {
        List<UiLogEntry>? toFlush;
        lock (m_PreAttachLock)
        {
            if (m_DispatcherQueue != null) { return; }
            m_DispatcherQueue = i_DispatcherQueue;
            toFlush = m_PreAttachBuffer;
            m_PreAttachBuffer = null;
        }

        if (toFlush == null || toFlush.Count == 0) { return; }
        // Flush on the UI thread so Entries is mutated only on its owning thread. If HasThreadAccess (Attach called from UI), the callback runs synchronously inside TryEnqueue's invocation in WinUI's dispatcher impl; otherwise it's posted
        i_DispatcherQueue.TryEnqueue(() =>
        {
            if (!m_AcceptingRecords) { return; }
            for (int i = 0; i < toFlush.Count; i++) { Add(toFlush[i]); }
        });
    }

    // Disables UI log mutation. Must be called BEFORE any host-shutdown await in the app's close path so that the flurry of "Disconnecting ..." log lines from hosted services on shutdown can't land on a torn-down view tree. Safe to call from the UI thread; safe to call multiple times (idempotent). After this returns, RecordWritten is unsubscribed and any already-queued dispatcher callbacks will check the flag and bail
    public void BeginShutdown()
    {
        m_AcceptingRecords = false;
        try { m_LogService.RecordWritten -= OnRecordWritten; }
        catch { /* unsubscribe is best-effort; harmless if already gone */ }
    }

    // Clears the UI log buffer
    public void Clear()
    {
        DispatcherQueue? dispatcher = m_DispatcherQueue;
        if (dispatcher == null)
        {
            // Pre-attach: just drop the buffered entries; Entries itself has no contents yet
            lock (m_PreAttachLock) { m_PreAttachBuffer?.Clear(); }
            return;
        }
        if (dispatcher.HasThreadAccess) { Entries.Clear(); return; }
        dispatcher.TryEnqueue(() =>
        {
            if (!m_AcceptingRecords) { return; }
            Entries.Clear();
        });
    }

    // Removes a single entry from the buffer
    public void Remove(UiLogEntry i_Entry)
    {
        DispatcherQueue? dispatcher = m_DispatcherQueue;
        if (dispatcher == null)
        {
            lock (m_PreAttachLock) { m_PreAttachBuffer?.Remove(i_Entry); }
            return;
        }
        if (dispatcher.HasThreadAccess) { Entries.Remove(i_Entry); return; }
        dispatcher.TryEnqueue(() =>
        {
            if (!m_AcceptingRecords) { return; }
            Entries.Remove(i_Entry);
        });
    }

    // Handles incoming log records from the logger. Fires synchronously on whichever thread called Info/Warn/Error/Debug -- could be UI, the SDK packet thread, a hosted service background thread, etc. All paths funnel mutations of Entries onto the UI thread; flag checked at entry AND inside the dispatched callback to seal the shutdown window
    private void OnRecordWritten(LogRecord i_Record)
    {
        if (!m_AcceptingRecords) { return; }

        UiLogEntry entry = new UiLogEntry(i_Record);
        DispatcherQueue? dispatcher = m_DispatcherQueue;

        if (dispatcher == null)
        {
            // Pre-attach window: queue under lock; cap at m_MaxEntries so a never-attaching dispatcher (test harness, headless shutdown) can't leak unbounded
            lock (m_PreAttachLock)
            {
                // Re-check under lock: AttachUiDispatcher may have nulled m_PreAttachBuffer between our read above and acquiring the lock
                if (m_DispatcherQueue == null)
                {
                    if (m_PreAttachBuffer == null) { return; }
                    m_PreAttachBuffer.Add(entry);
                    if (m_PreAttachBuffer.Count > m_MaxEntries) { m_PreAttachBuffer.RemoveAt(0); }
                    return;
                }
                dispatcher = m_DispatcherQueue;
            }
        }

        if (dispatcher.HasThreadAccess) { Add(entry); return; }
        // The callback re-checks m_AcceptingRecords. Between TryEnqueue posting and the dispatcher running the callback, BeginShutdown may have fired -- without this guard, a queued Add could run after the view tree is gone
        dispatcher.TryEnqueue(() =>
        {
            if (!m_AcceptingRecords) { return; }
            Add(entry);
        });
    }

    // Adds an entry and enforces bounded size. ONLY safe to call on the UI thread (mutates Entries which is data-bound and not thread-safe). Every external caller funnels through OnRecordWritten / AttachUiDispatcher which guarantee this
    private void Add(UiLogEntry i_Entry)
    {
        Entries.Add(i_Entry);
        if (Entries.Count > m_MaxEntries) { Entries.RemoveAt(0); }
    }
    #endregion
}
