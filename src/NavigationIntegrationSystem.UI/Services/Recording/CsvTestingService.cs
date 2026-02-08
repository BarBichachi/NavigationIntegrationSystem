// FILE: src\NavigationIntegrationSystem.UI\Services\Recording\CsvTestingService.cs
using System;
using System.IO;
using System.Text;
using NavigationIntegrationSystem.Core.Logging;
using Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;

namespace NavigationIntegrationSystem.UI.Services.Recording;

public sealed class CsvTestingService : IDisposable
{
    private readonly ILogService m_LogService;
    private StreamWriter? m_CsvWriter;
    private bool m_IsRecording;

    public CsvTestingService(ILogService i_LogService)
    {
        m_LogService = i_LogService;
    }

    public void Start()
    {
        if (m_IsRecording) return;

        try
        {
            // Absolute fallback to ensure visibility
            string recPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings");
            if (!Directory.Exists(recPath)) Directory.CreateDirectory(recPath);

            string filename = $"TestLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.Combine(recPath, filename);

            // We use FileOptions.WriteThrough to force the OS to bypass all caches
            var fs = new FileStream(fullPath,
                                    FileMode.Create,
                                    FileAccess.Write,
                                    FileShare.ReadWrite,
                                    4096,
                                    FileOptions.WriteThrough);

            m_CsvWriter = new StreamWriter(fs, Encoding.UTF8);
            m_CsvWriter.AutoFlush = true; // Force flush on every WriteLine

            WriteHeader();
            m_IsRecording = true;

            m_LogService.Info(nameof(CsvTestingService), $"SUCCESS: CSV created at {fullPath}");
        }
        catch (Exception ex)
        {
            m_LogService.Error(nameof(CsvTestingService), "CRITICAL: CSV Start failed", ex);
        }
    }

    public void PrintSnapshot(IntegratedInsOutput_Data i_Data)
    {
        if (!m_IsRecording || m_CsvWriter == null) return;

        var sb = new StringBuilder();
        sb.Append($"{DateTime.Now:HH:mm:ss.fff},");

        // Helper to format the triplets (DeviceCode, ID, Value)
        Action<IntegratedValueTriplet> append = (t) => sb.Append($"{t.DeviceCode},{t.DeviceId},{t.Value:F8},");

        append(i_Data.PositionLat);
        append(i_Data.PositionLon);
        append(i_Data.PositionAlt);
        append(i_Data.EulerRoll);
        append(i_Data.EulerPitch);
        append(i_Data.EulerAzimuth);
        append(i_Data.VelocityNorth);
        append(i_Data.VelocityEast);
        append(i_Data.VelocityDown);

        m_CsvWriter.WriteLine(sb.ToString().TrimEnd(','));

        // Critical: Flush both the writer and the stream so the file appears in Explorer
        m_CsvWriter.Flush();
        m_CsvWriter.BaseStream.Flush();
    }

    private void WriteHeader()
    {
        m_CsvWriter?.WriteLine("Time,Lat_C,Lat_ID,Lat_V,Lon_C,Lon_ID,Lon_V,Alt_C,Alt_ID,Alt_V,Roll_C,Roll_ID,Roll_V,Pitch_C,Pitch_ID,Pitch_V,Azi_C,Azi_ID,Azi_V,VN_C,VN_ID,VN_V,VE_C,VE_ID,VE_V,VD_C,VD_ID,VD_V");
    }

    public void Stop()
    {
        if (!m_IsRecording) return;
        m_CsvWriter?.Dispose();
        m_CsvWriter = null;
        m_IsRecording = false;
    }

    public void Dispose() => Stop();
}