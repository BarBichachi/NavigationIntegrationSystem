// FILE: src\NavigationIntegrationSystem.Infrastructure\Recording\NisRecordingService.cs
using System;
using System.IO;
using System.Threading;
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
    #endregion

    #region Private Fields
    private readonly BinaryFileRecorderEnhanced m_Recorder;
    private readonly ILogService m_LogService;
    private readonly object m_Lock = new();
    private byte[] m_EncodeBuffer;
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
    // Starts recording and immediately writes a "Fill" record to prevent 0-byte deletion
    public void Start()
    {
        lock (m_Lock)
        {
            if (IsRecording) return;

            string result = m_Recorder.StartRecording();
            if (string.IsNullOrEmpty(result))
            {
                // IMMEDIATELY write a dummy record to increment m_CurrentFileSize
                // This ensures the legacy CloseCurrentFile never sees a 0 size.
                m_Recorder.Record(99, 99, new byte[1], 1, DateTime.UtcNow);
                m_LogService.Info(nameof(NisRecordingService), "Recording started with persistence fill.");
            }
            else
            {
                m_LogService.Error(nameof(NisRecordingService), $"Start failed: {result}");
            }
        }
    }

    // Flushes and stops the recording
    public void Stop()
    {
        lock (m_Lock)
        {
            if (!IsRecording) return;

            // FORCE a flush using the legacy periodic logic
            // This triggers m_File.Flush() inside the legacy class
            m_Recorder.OnPeriodicOnePps(this, EventArgs.Empty);

            // Give the OS a moment to finish the Async Write
            Thread.Sleep(50);

            m_Recorder.StopRecording();
            m_LogService.Info(nameof(NisRecordingService), "Recording stopped and flushed.");
        }
    }

    // Records live snapshots
    public void RecordIntegratedOutput(object i_DataSnapshot)
    {
        if (!IsRecording || i_DataSnapshot is not IntegratedInsOutput_Data data) return;

        lock (m_Lock)
        {
            int size = 0;
            EncodeDataToBuffer(data, ref m_EncodeBuffer, ref size);

            if (size > 0)
            {
                bool success = m_Recorder.Record(
                    c_IntegratedOutputRecordId,
                    c_IntegratedOutputRecordType,
                    m_EncodeBuffer,
                    size,
                    DateTime.UtcNow);

                // Periodic flush to keep the file size updated on disk
                // We do this every record for safety in NIS (usually 50Hz)
                if (success)
                {
                    m_Recorder.OnPeriodicOnePps(this, EventArgs.Empty);
                }
            }
        }
    }

    // Manual encoding for VIC protocol [Sync][Payload][Checksum]
    private void EncodeDataToBuffer(IntegratedInsOutput_Data i_Data, ref byte[] io_Buffer, ref int io_Size)
    {
        // Clear buffer to ensure no trailing garbage
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