// FILE: src\NavigationIntegrationSystem.UI\Services\Recording\CsvTestingService.cs
using System;
using System.Globalization;
using System.IO;
using System.Text;
using NavigationIntegrationSystem.Core.Logging;
using Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;

namespace NavigationIntegrationSystem.UI.Services.Recording;

public sealed class CsvTestingService : IDisposable
{
    #region Constants
    // 64KB buffer is large enough that at 100Hz we flush a few times per minute on Stop only
    private const int c_FileBufferBytes = 64 * 1024;
    #endregion

    #region Private Fields
    private readonly ILogService m_LogService;
    private readonly object m_Lock = new();
    private StreamWriter? m_CsvWriter;
    private bool m_IsRecording;
    #endregion

    #region Constructors
    public CsvTestingService(ILogService i_LogService)
    {
        m_LogService = i_LogService;
    }
    #endregion

    #region Functions
    // Opens the CSV file with a buffered writer; flushing is deferred until Stop/Dispose
    public void Start()
    {
        lock (m_Lock)
        {
            if (m_IsRecording) return;

            try
            {
                string recPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings");
                if (!Directory.Exists(recPath)) Directory.CreateDirectory(recPath);

                string filename = $"TestLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullPath = Path.Combine(recPath, filename);

                // Standard buffered FileStream; no WriteThrough, no AutoFlush; flush deferred until Stop
                FileStream fs = new FileStream(fullPath,
                                               FileMode.Create,
                                               FileAccess.Write,
                                               FileShare.Read,
                                               c_FileBufferBytes,
                                               FileOptions.SequentialScan);

                m_CsvWriter = new StreamWriter(fs, Encoding.UTF8, c_FileBufferBytes) { AutoFlush = false };

                WriteHeader();
                m_IsRecording = true;

                m_LogService.Info(nameof(CsvTestingService), $"CSV started at {fullPath}");
            }
            catch (Exception ex)
            {
                m_LogService.Error(nameof(CsvTestingService), "CSV Start failed", ex);
            }
        }
    }

    // Writes a single snapshot row; no flush (deferred to Stop). Thread-safe.
    public void PrintSnapshot(IntegratedInsOutput_Data i_Data)
    {
        lock (m_Lock)
        {
            if (!m_IsRecording || m_CsvWriter == null) return;

            StringBuilder sb = new StringBuilder(256);
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));

            AppendTriplet(sb, i_Data.PositionLat);
            AppendTriplet(sb, i_Data.PositionLon);
            AppendTriplet(sb, i_Data.PositionAlt);
            AppendTriplet(sb, i_Data.EulerRoll);
            AppendTriplet(sb, i_Data.EulerPitch);
            AppendTriplet(sb, i_Data.EulerAzimuth);
            AppendTriplet(sb, i_Data.VelocityNorth);
            AppendTriplet(sb, i_Data.VelocityEast);
            AppendTriplet(sb, i_Data.VelocityDown);

            m_CsvWriter.WriteLine(sb.ToString());
        }
    }

    // Appends ",Code,Id,Value" with invariant formatting
    private static void AppendTriplet(StringBuilder io_Sb, IntegratedValueTriplet i_Triplet)
    {
        io_Sb.Append(',').Append(i_Triplet.DeviceCode)
             .Append(',').Append(i_Triplet.DeviceId)
             .Append(',').Append(i_Triplet.Value.ToString("F8", CultureInfo.InvariantCulture));
    }

    // Writes the static header row once on Start
    private void WriteHeader()
    {
        m_CsvWriter?.WriteLine("Time,Lat_C,Lat_ID,Lat_V,Lon_C,Lon_ID,Lon_V,Alt_C,Alt_ID,Alt_V,Roll_C,Roll_ID,Roll_V,Pitch_C,Pitch_ID,Pitch_V,Azi_C,Azi_ID,Azi_V,VN_C,VN_ID,VN_V,VE_C,VE_ID,VE_V,VD_C,VD_ID,VD_V");
    }

    // Flushes and closes the writer
    public void Stop()
    {
        lock (m_Lock)
        {
            if (!m_IsRecording) return;
            try
            {
                m_CsvWriter?.Flush();
            }
            catch (Exception ex)
            {
                m_LogService.Error(nameof(CsvTestingService), "CSV flush failed on Stop", ex);
            }
            m_CsvWriter?.Dispose();
            m_CsvWriter = null;
            m_IsRecording = false;
        }
    }

    public void Dispose() => Stop();
    #endregion
}
