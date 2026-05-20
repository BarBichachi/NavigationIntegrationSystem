using Microsoft.Extensions.Hosting;
using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Integration;
using NavigationIntegrationSystem.Core.Recording;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Layout;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Pages;
using Infrastructure.DataStructures;
using Infrastructure.Navigation;
using Infrastructure.Navigation.EulerCalculations;
using Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.Services.Recording;

public sealed class IntegrationSnapshotService : IHostedService, IDisposable
{
    #region Constants
    private const int c_SnapshotIntervalMs = 10; // 100Hz
    #endregion

    #region Private Fields
    private readonly IRecordingService m_RecordingService;
    private readonly IInsDeviceInstanceProvider m_IdProvider;
    private readonly IntegrationViewModel m_IntegrationVm;
    private readonly CsvTestingService m_CsvTester;
    private readonly object m_Lock = new();

    private IntegrationFieldRowViewModel[] m_RowsCache = Array.Empty<IntegrationFieldRowViewModel>();
    private CancellationTokenSource? m_LoopCts;
    private Task? m_LoopTask;
    #endregion

    #region Constructors
    public IntegrationSnapshotService(IRecordingService i_RecordingService, IInsDeviceInstanceProvider i_IdProvider,
        IntegrationViewModel i_IntegrationVm, CsvTestingService i_CsvTester)
    {
        m_RecordingService = i_RecordingService;
        m_IdProvider = i_IdProvider;
        m_IntegrationVm = i_IntegrationVm;
        m_CsvTester = i_CsvTester;
    }
    #endregion

    #region Functions
    // Captures the stable Rows array on Start (Rows is built once at IntegrationViewModel construction and never replaced).
    public Task StartAsync(CancellationToken cancellationToken)
    {
        IntegrationFieldRowViewModel[] cache = new IntegrationFieldRowViewModel[m_IntegrationVm.Rows.Count];
        for (int i = 0; i < m_IntegrationVm.Rows.Count; i++)
        {
            cache[i] = m_IntegrationVm.Rows[i];
        }
        m_RowsCache = cache;

        m_RecordingService.RecordingStateChanged += OnRecordingStateChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        m_RecordingService.RecordingStateChanged -= OnRecordingStateChanged;
        StopLoop();
        m_CsvTester.Stop();
        return Task.CompletedTask;
    }

    // Reacts to recording state transitions; safe to invoke from any thread
    private void OnRecordingStateChanged(object? sender, bool i_IsRecording)
    {
        if (i_IsRecording)
        {
            m_CsvTester.Start();
            StartLoop();
        }
        else
        {
            StopLoop();
            m_CsvTester.Stop();
        }
    }

    // Spins up the background snapshot loop. Idempotent — if a loop is already running, returns.
    private void StartLoop()
    {
        lock (m_Lock)
        {
            if (m_LoopTask != null) { return; }
            m_LoopCts = new CancellationTokenSource();
            CancellationToken token = m_LoopCts.Token;
            m_LoopTask = Task.Run(() => SnapshotLoopAsync(token));
        }
    }

    // Cancels and awaits the background loop; safe to call multiple times
    private void StopLoop()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (m_Lock)
        {
            cts = m_LoopCts;
            task = m_LoopTask;
            m_LoopCts = null;
            m_LoopTask = null;
        }

        try
        {
            cts?.Cancel();
            task?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException) { /* cancellation */ }
        finally
        {
            cts?.Dispose();
        }
    }

    // Background 100Hz loop: ticks via PeriodicTimer (independent of UI thread), captures snapshots, emits records.
    private async Task SnapshotLoopAsync(CancellationToken i_Token)
    {
        try
        {
            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(c_SnapshotIntervalMs));
            while (await timer.WaitForNextTickAsync(i_Token).ConfigureAwait(false))
            {
                if (!m_RecordingService.IsRecording) { continue; }
                TakeSnapshot();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            // Swallow to avoid taking down the host; the recording service will keep running and the user can stop+restart recording
        }
    }

    // Captures one snapshot: builds an IntegratedInsOutput_Data using exactly one clone-read/clone-write per Position/Euler/Velocity group (mitigation for the TO_BE_DELETED clone-on-accessor pattern), then submits the record.
    private void TakeSnapshot()
    {
        IntegratedInsOutput_Data data = new IntegratedInsOutput_Data();

        // Clone-read each compound field exactly ONCE per snapshot
        WGS84Data position = data.Position;
        EulerData euler = data.EulerData;
        NEDData velocity = data.VelocityVector;

        IntegrationFieldRowViewModel[] rows = m_RowsCache;
        for (int i = 0; i < rows.Length; i++)
        {
            IntegrationRowSnapshot snap = rows[i].CaptureSnapshotForRecording();
            if (!snap.HasSelection) { continue; }

            ushort code = (ushort)snap.DeviceType;
            ushort id = snap.SourceDevice != null ? m_IdProvider.GetInstanceId(snap.SourceDevice) : (ushort)0;

            ApplyRowSnapshot(snap.FieldName, snap.Value, code, id, data, position, euler, velocity);
        }

        // Clone-write each compound field exactly ONCE per snapshot
        data.Position = position;
        data.EulerData = euler;
        data.VelocityVector = velocity;

        // OutputTime: snapshot capture time (Phase 3 will route from source when Playback is the selected device)
        data.OutputTimeDeviceCode = 0;
        data.OutputTimeDeviceId = 0;
        data.OutputTime = DateTime.UtcNow;
        data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.OutputTimeValid;

        // Velocity Total inherits the North component's source metadata; magnitude is derived inside IntegratedInsOutput_Data
        data.VelocityTotalDeviceCode = data.VelocityNorthDeviceCode;
        data.VelocityTotalDeviceId = data.VelocityNorthDeviceId;

        m_RecordingService.RecordIntegratedOutput(data);
        m_CsvTester.PrintSnapshot(data);
    }

    // Routes a single row snapshot to the right scalar field on io_Data; compound fields are mutated on the supplied locals (no per-row clone).
    private static void ApplyRowSnapshot(string i_Name, double i_Val, ushort i_Code, ushort i_Id, IntegratedInsOutput_Data io_Data,
                                         WGS84Data io_Position, EulerData io_Euler, NEDData io_Velocity)
    {
        switch (i_Name)
        {
            case IntegrationFieldNames.Latitude:
                io_Data.LatitudeDeviceCode = i_Code;
                io_Data.LatitudeDeviceId = i_Id;
                io_Position.Lat = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PositionLatValid;
                break;
            case IntegrationFieldNames.Longitude:
                io_Data.LongitudeDeviceCode = i_Code;
                io_Data.LongitudeDeviceId = i_Id;
                io_Position.Lon = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PositionLonValid;
                break;
            case IntegrationFieldNames.Altitude:
                io_Data.AltitudeDeviceCode = i_Code;
                io_Data.AltitudeDeviceId = i_Id;
                io_Position.Alt = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PositionAltValid;
                break;

            // Euler angle arms. Inner field/flag names keep the "Azimuth" spelling — that's the
            // parent solution's binary record contract (TO_BE_DELETED-rooted). NIS internally
            // calls this field "Yaw" because that's what VN310 (and the underlying sensor
            // convention) emits.
            case IntegrationFieldNames.Yaw:
                io_Data.AzimuthDeviceCode = i_Code;
                io_Data.AzimuthDeviceId = i_Id;
                io_Euler.Angles.Yaw = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.AzimuthValid;
                break;
            case IntegrationFieldNames.Pitch:
                io_Data.PitchDeviceCode = i_Code;
                io_Data.PitchDeviceId = i_Id;
                io_Euler.Angles.Pitch = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PitchValid;
                break;
            case IntegrationFieldNames.Roll:
                io_Data.RollDeviceCode = i_Code;
                io_Data.RollDeviceId = i_Id;
                io_Euler.Angles.Roll = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.RollValid;
                break;
            case IntegrationFieldNames.YawRate:
                io_Data.AzimuthRateDeviceCode = i_Code;
                io_Data.AzimuthRateDeviceId = i_Id;
                io_Euler.Rates.Yaw = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.AzimuthRateValid;
                break;
            case IntegrationFieldNames.PitchRate:
                io_Data.PitchRateDeviceCode = i_Code;
                io_Data.PitchRateDeviceId = i_Id;
                io_Euler.Rates.Pitch = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PitchRateValid;
                break;
            case IntegrationFieldNames.RollRate:
                io_Data.RollRateDeviceCode = i_Code;
                io_Data.RollRateDeviceId = i_Id;
                io_Euler.Rates.Roll = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.RollRateValid;
                break;

            case IntegrationFieldNames.VelocityNorth:
                io_Data.VelocityNorthDeviceCode = i_Code;
                io_Data.VelocityNorthDeviceId = i_Id;
                io_Velocity.North = i_Val;
                io_Data.StatusValue |= (uint)(IntegratedInsOutputStatusFlags.VelocityNorthValid | IntegratedInsOutputStatusFlags.VelocityTotalValid);
                break;
            case IntegrationFieldNames.VelocityEast:
                io_Data.VelocityEastDeviceCode = i_Code;
                io_Data.VelocityEastDeviceId = i_Id;
                io_Velocity.East = i_Val;
                io_Data.StatusValue |= (uint)(IntegratedInsOutputStatusFlags.VelocityEastValid | IntegratedInsOutputStatusFlags.VelocityTotalValid);
                break;
            case IntegrationFieldNames.VelocityDown:
                io_Data.VelocityDownDeviceCode = i_Code;
                io_Data.VelocityDownDeviceId = i_Id;
                io_Velocity.Down = i_Val;
                io_Data.StatusValue |= (uint)(IntegratedInsOutputStatusFlags.VelocityDownValid | IntegratedInsOutputStatusFlags.VelocityTotalValid);
                break;

            case IntegrationFieldNames.Course:
                io_Data.CourseDeviceCode = i_Code;
                io_Data.CourseDeviceId = i_Id;
                io_Data.Course = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.CourseValid;
                break;
        }
    }

    public void Dispose()
    {
        m_RecordingService.RecordingStateChanged -= OnRecordingStateChanged;
        StopLoop();
    }
    #endregion
}
