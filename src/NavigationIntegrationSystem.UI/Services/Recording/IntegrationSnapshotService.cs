using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Recording;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Layout;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Pages;
using Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.Services.Recording;

public sealed class IntegrationSnapshotService : IHostedService, IDisposable
{
    #region Private Fields
    private readonly IRecordingService m_RecordingService;
    private readonly IInsDeviceInstanceProvider m_IdProvider;
    private readonly IntegrationViewModel m_IntegrationVm;
    private readonly CsvTestingService m_CsvTester;
    private DispatcherQueueTimer? m_SnapshotTimer;
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
    // On Start, subscribes to recording state changes to manage snapshot timer and recording flow
    public Task StartAsync(CancellationToken cancellationToken)
    {
        m_RecordingService.RecordingStateChanged += OnRecordingStateChanged;
        return Task.CompletedTask;
    }

    // On Stop, unsubscribes from recording state changes and ensures timer is stopped to clean up resources
    public Task StopAsync(CancellationToken cancellationToken)
    {
        m_RecordingService.RecordingStateChanged -= OnRecordingStateChanged;
        StopTimer();
        m_CsvTester.Stop();
        return Task.CompletedTask;
    }

    // Central handler for recording state changes; starts/stops snapshot timer and recording flow accordingly
    private void OnRecordingStateChanged(object? sender, bool i_IsRecording)
    {
        if (i_IsRecording)
        {
            m_CsvTester.Start();
            StartTimer();
        }
        else
        {
            StopTimer();
            m_CsvTester.Stop();
        }
    }

    // Initializes and starts the snapshot timer if not already running; ensures it runs on the UI thread for safe VM access
    private void StartTimer()
    {
        if (m_SnapshotTimer != null) return;

        // Ensure we are on the UI thread for VM access
        m_SnapshotTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        m_SnapshotTimer.Interval = TimeSpan.FromMilliseconds(10); // 100Hz
        m_SnapshotTimer.Tick += (s, e) => TakeSnapshot();
        m_SnapshotTimer.Start();
    }

    // Stops the snapshot timer if it is running and cleans up the reference
    private void StopTimer()
    {
        m_SnapshotTimer?.Stop();
        m_SnapshotTimer = null;
    }

    // Concised one liner comment
    // Core function that takes a snapshot of the current integration state
    private void TakeSnapshot()
    {
        if (!m_RecordingService.IsRecording) return;

        IntegratedInsOutput_Data data = new IntegratedInsOutput_Data();

        foreach (IntegrationFieldRowViewModel row in m_IntegrationVm.Rows)
        {
            MapRowToData(row, data);
        }

        // Policy: Velocity Total is derived from components
        var vel = data.VelocityVector;
        double total = Math.Sqrt(vel.North * vel.North + vel.East * vel.East + vel.Down * vel.Down);

        // Metadata for Total follows the North component source
        data.VelocityTotalDeviceCode = data.VelocityNorthDeviceCode;
        data.VelocityTotalDeviceId = data.VelocityNorthDeviceId;

        m_RecordingService.RecordIntegratedOutput(data);
        m_CsvTester.PrintSnapshot(data);
    }

    // Core mapping entry point for a single row
    private void MapRowToData(IntegrationFieldRowViewModel i_Row, IntegratedInsOutput_Data io_Data)
    {
        IntegrationSourceCandidateViewModel? selected = i_Row.VisibleSources.FirstOrDefault(s => s.IsSelected);
        if (selected == null) return;

        ushort code = (ushort)selected.DeviceType;
        ushort id = selected.SourceDevice != null ? m_IdProvider.GetInstanceId(selected.SourceDevice) : (ushort)0;
        double val = (selected is NumericSourceCandidateViewModel numeric) ? numeric.Value :
                     (selected is ManualSourceCandidateViewModel manual) ? manual.Value : 0;

        // Route to specialized mapping sub-functions
        MapPositionFields(i_Row.FieldName, val, code, id, io_Data);
        MapEulerFields(i_Row.FieldName, val, code, id, io_Data);
        MapVelocityFields(i_Row.FieldName, val, code, id, io_Data);
        MapMiscFields(i_Row.FieldName, val, code, id, io_Data);
    }

    // Handles Latitude, Longitude, and Altitude
    private void MapPositionFields(string i_Name, double i_Val, ushort i_Code, ushort i_Id, IntegratedInsOutput_Data io_Data)
    {
        var pos = io_Data.Position;
        switch (i_Name)
        {
            case "Latitude":
                io_Data.LatitudeDeviceCode = i_Code; io_Data.LatitudeDeviceId = i_Id;
                pos.Lat = i_Val; io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PositionLatValid;

                // Output Time Source is tied to Latitude selection
                io_Data.OutputTimeDeviceCode = i_Code;
                io_Data.OutputTimeDeviceId = i_Id;
                io_Data.OutputTime = DateTime.UtcNow;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.OutputTimeValid;
                break;

            case "Longitude":
                io_Data.LongitudeDeviceCode = i_Code;
                io_Data.LongitudeDeviceId = i_Id;
                pos.Lon = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PositionLonValid;
                break;

            case "Altitude":
                io_Data.AltitudeDeviceCode = i_Code;
                io_Data.AltitudeDeviceId = i_Id;
                pos.Alt = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PositionAltValid;
                break;
        }
        io_Data.Position = pos;
    }

    // Handles Roll, Pitch, Azimuth and their respective Rates
    private void MapEulerFields(string i_Name, double i_Val, ushort i_Code, ushort i_Id, IntegratedInsOutput_Data io_Data)
    {
        var euler = io_Data.EulerData;
        switch (i_Name)
        {
            case "Roll":
                io_Data.RollDeviceCode = i_Code;
                io_Data.RollDeviceId = i_Id;
                euler.Angles.Roll = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.RollValid;
                break;
            case "Pitch":
                io_Data.PitchDeviceCode = i_Code;
                io_Data.PitchDeviceId = i_Id;
                euler.Angles.Pitch = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PitchValid;
                break;
            case "Azimuth":
                io_Data.AzimuthDeviceCode = i_Code;
                io_Data.AzimuthDeviceId = i_Id;
                euler.Angles.Yaw = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.AzimuthValid;
                break;
            case "Roll Rate":
                io_Data.RollRateDeviceCode = i_Code;
                io_Data.RollRateDeviceId = i_Id;
                euler.Rates.Roll = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.RollRateValid;
                break;
            case "Pitch Rate":
                io_Data.PitchRateDeviceCode = i_Code;
                io_Data.PitchRateDeviceId = i_Id;
                euler.Rates.Pitch = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.PitchRateValid;
                break;
            case "Azimuth Rate":
                io_Data.AzimuthRateDeviceCode = i_Code;
                io_Data.AzimuthRateDeviceId = i_Id;
                euler.Rates.Yaw = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.AzimuthRateValid;
                break;
        }
        io_Data.EulerData = euler;
    }

    // Handles Velocity components and triggers Total Velocity validity
    private void MapVelocityFields(string i_Name, double i_Val, ushort i_Code, ushort i_Id, IntegratedInsOutput_Data io_Data)
    {
        var vel = io_Data.VelocityVector;
        bool isVelocityComponent = false;

        switch (i_Name)
        {
            case "Velocity North":
                io_Data.VelocityNorthDeviceCode = i_Code;
                io_Data.VelocityNorthDeviceId = i_Id;
                vel.North = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.VelocityNorthValid;
                isVelocityComponent = true;
                break;
            case "Velocity East":
                io_Data.VelocityEastDeviceCode = i_Code;
                io_Data.VelocityEastDeviceId = i_Id;
                vel.East = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.VelocityEastValid;
                isVelocityComponent = true;
                break;
            case "Velocity Down":
                io_Data.VelocityDownDeviceCode = i_Code;
                io_Data.VelocityDownDeviceId = i_Id;
                vel.Down = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.VelocityDownValid;
                isVelocityComponent = true;
                break;
        }

        if (isVelocityComponent)
        {
            io_Data.VelocityVector = vel;
            // Mark Total as valid since it is calculated from these components
            io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.VelocityTotalValid;
        }
    }

    // Handles Course and other miscellaneous protocol fields
    private void MapMiscFields(string i_Name, double i_Val, ushort i_Code, ushort i_Id, IntegratedInsOutput_Data io_Data)
    {
        switch (i_Name)
        {
            case "Course":
                io_Data.CourseDeviceCode = i_Code;
                io_Data.CourseDeviceId = i_Id;
                io_Data.Course = i_Val;
                io_Data.StatusValue |= (uint)IntegratedInsOutputStatusFlags.CourseValid;
                break;
        }
    }

    // Implements IDisposable to ensure proper cleanup of event subscriptions and timers when the service is disposed
    public void Dispose()
    {
        m_RecordingService.RecordingStateChanged -= OnRecordingStateChanged;
        StopTimer();
    }
    #endregion
}