// FILE: src\NavigationIntegrationSystem.Infrastructure\Recording\NisRecordingService.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Recording;
using Infrastructure.FileManagement.DataRecording;
using Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;
using Infrastructure.Templates;
using log4net;

namespace NavigationIntegrationSystem.Infrastructure.Recording;

public sealed class NisRecordingService : IRecordingService
{
    #region Constants
    private const int c_IntegratedOutputRecordType = 50;
    private const int c_IntegratedOutputRecordId = 0;
    private const int c_LegacyBufferSize = 1024;
    private const int c_FlushIntervalMs = 1000;
    #endregion

    #region Private Fields
    private readonly BinaryFileRecorderEnhanced m_Recorder;
    private readonly ILogService m_LogService;
    private readonly object m_Lock = new();
    private byte[] m_EncodeBuffer;
    private CancellationTokenSource? m_FlushCts;
    private Task? m_FlushTask;
    #endregion

    #region Properties
    public bool IsRecording => m_Recorder.isRecording;
    #endregion

    #region Events
    public event EventHandler<bool>? RecordingStateChanged;
    #endregion

    #region Constructors
    public NisRecordingService(ILogService i_LogService, ILogPaths i_LogPaths)
    {
        m_LogService = i_LogService;
        m_EncodeBuffer = new byte[c_LegacyBufferSize];

        m_Recorder = Singleton<BinaryFileRecorderEnhanced>.Instance;
        m_Recorder.Logger = new Log4NetAdapter(i_LogService, "BinaryFileRecorder");

        string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        string recPath = Path.Combine(baseDir, "Recordings");
        m_Recorder.SetRecordingDirectory(recPath);

        m_Recorder.RecordingStartStop += (isRec) => RecordingStateChanged?.Invoke(this, isRec);
    }
    #endregion

    #region Functions
    // Starts recording, writes a 1-byte filler record (prevents legacy 0-byte deletion),
    // and spins up a 1Hz background flush loop so the file size updates on disk while recording.
    public void Start()
    {
        lock (m_Lock)
        {
            if (IsRecording) return;

            string result = m_Recorder.StartRecording();
            if (!string.IsNullOrEmpty(result))
            {
                m_LogService.Error(nameof(NisRecordingService), $"Start failed: {result}");
                return;
            }

            // Filler record: forces m_CurrentFileSize > 0 so legacy CloseCurrentFile never deletes
            m_Recorder.Record(99, 99, new byte[1], 1, DateTime.UtcNow);

            m_FlushCts = new CancellationTokenSource();
            m_FlushTask = Task.Run(() => PeriodicFlushLoopAsync(m_FlushCts.Token));

            m_LogService.Info(nameof(NisRecordingService), "Recording started.");
        }
    }

    // Stops the periodic flusher, forces a final flush, and stops the recorder
    public void Stop()
    {
        CancellationTokenSource? ctsToCancel = null;
        Task? taskToAwait = null;

        lock (m_Lock)
        {
            if (!IsRecording) return;

            ctsToCancel = m_FlushCts;
            taskToAwait = m_FlushTask;
            m_FlushCts = null;
            m_FlushTask = null;
        }

        try
        {
            ctsToCancel?.Cancel();
            taskToAwait?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException) { /* cancellation */ }
        finally
        {
            ctsToCancel?.Dispose();
        }

        lock (m_Lock)
        {
            // Final flush — drains the legacy recorder's buffer to disk
            m_Recorder.OnPeriodicOnePps(this, EventArgs.Empty);

            // Legacy BinaryFileRecorderEnhanced.Record issues fire-and-forget WriteAsync;
            // brief sleep lets the in-flight async write land before StopRecording closes the handle.
            // (Phase 4 wraps this hack behind a proper async interface.)
            Thread.Sleep(50);

            m_Recorder.StopRecording();
            m_LogService.Info(nameof(NisRecordingService), "Recording stopped and flushed.");
        }
    }

    // Encodes a snapshot and writes it. No per-record flush — flushing happens at 1Hz via PeriodicFlushLoopAsync.
    public void RecordIntegratedOutput(object i_DataSnapshot)
    {
        if (!IsRecording || i_DataSnapshot is not IntegratedInsOutput_Data data) return;

        lock (m_Lock)
        {
            int size = 0;
            EncodeDataToBuffer(data, ref m_EncodeBuffer, ref size);

            if (size > 0)
            {
                m_Recorder.Record(
                    c_IntegratedOutputRecordId,
                    c_IntegratedOutputRecordType,
                    m_EncodeBuffer,
                    size,
                    DateTime.UtcNow);
            }
        }
    }

    // 1Hz background flush so file size updates in Explorer / file system while recording is ongoing
    private async Task PeriodicFlushLoopAsync(CancellationToken i_Token)
    {
        try
        {
            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(c_FlushIntervalMs));
            while (await timer.WaitForNextTickAsync(i_Token).ConfigureAwait(false))
            {
                lock (m_Lock)
                {
                    if (!IsRecording) return;
                    m_Recorder.OnPeriodicOnePps(this, EventArgs.Empty);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            m_LogService.Error(nameof(NisRecordingService), "Periodic flush loop error", ex);
        }
    }

    // Manual encoding for VIC protocol [Sync][Payload][Checksum]
    private void EncodeDataToBuffer(IntegratedInsOutput_Data i_Data, ref byte[] io_Buffer, ref int io_Size)
    {
        Array.Clear(io_Buffer, 0, io_Buffer.Length);

        using (MemoryStream ms = new MemoryStream(io_Buffer))
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)0x50);
            i_Data.Encode(writer);
            writer.Write((byte)0);
        }

        int totalLen = IntegratedInsOutput_Data.BinLength + 2;
        byte checksum = 0;
        for (int i = 1; i < totalLen - 1; i++)
        {
            checksum += io_Buffer[i];
        }

        io_Buffer[totalLen - 1] = checksum;
        io_Size = totalLen;
    }
    #endregion
}
