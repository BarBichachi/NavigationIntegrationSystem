using NavigationIntegrationSystem.Devices.Implementations.Vn310;

using System;
using System.Collections.Generic;
using System.Threading;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// Candidate backed by the live VN310 telemetry stream for a single scalar field.
// Packets arrive on the SDK's read thread; we write the latest value via Volatile.Write and
// deliberately do NOT fire PropertyChanged from there (WinUI bindings dislike cross-thread notifications).
// UI Tick (250ms timer) copies the latest value into the observable Value, firing PropertyChanged on UI.
// The 100Hz recording loop calls GetSnapshotValue() directly for the freshest read, bypassing UI cadence.
public sealed partial class Vn310SourceCandidateViewModel : IntegrationSourceCandidateViewModel, IDisposable
{
    #region Private Fields
    // Maps a Vn310DeviceModule field key to a strongly-typed accessor on Vn310Telemetry. Allocation-free per packet (the lambdas are cached static delegates after first reference)
    private static readonly IReadOnlyDictionary<string, Func<Vn310Telemetry, double>> s_FieldExtractors = new Dictionary<string, Func<Vn310Telemetry, double>>(StringComparer.Ordinal)
    {
        ["LatDeg"] = i_Telemetry => i_Telemetry.LatDeg,
        ["LonDeg"] = i_Telemetry => i_Telemetry.LonDeg,
        ["AltM"] = i_Telemetry => i_Telemetry.AltM,

        ["YawDeg"] = i_Telemetry => i_Telemetry.YawDeg,
        ["PitchDeg"] = i_Telemetry => i_Telemetry.PitchDeg,
        ["RollDeg"] = i_Telemetry => i_Telemetry.RollDeg,

        ["YawRateDegS"] = i_Telemetry => i_Telemetry.YawRateDegS,
        ["PitchRateDegS"] = i_Telemetry => i_Telemetry.PitchRateDegS,
        ["RollRateDegS"] = i_Telemetry => i_Telemetry.RollRateDegS,

        ["VelNorth"] = i_Telemetry => i_Telemetry.VelNorth,
        ["VelEast"] = i_Telemetry => i_Telemetry.VelEast,
        ["VelDown"] = i_Telemetry => i_Telemetry.VelDown,
    };

    private readonly Vn310InsDevice m_Device;
    private readonly Func<Vn310Telemetry, double> m_Extractor;
    private double m_LatestValue;
    private double m_DisplayedValue;
    private bool m_Disposed;
    #endregion

    #region Properties
    public double Value
    {
        get => m_DisplayedValue;
        private set
        {
            if (m_DisplayedValue == value) { return; }
            m_DisplayedValue = value;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public override string DisplayText => $"{m_DisplayedValue:0.00000}";
    #endregion

    #region Constructors
    public Vn310SourceCandidateViewModel(Vn310InsDevice i_Device, string i_DisplayName, string i_TelemetryFieldKey)
        : base(i_Device.Definition.Type, i_DisplayName)
    {
        if (!s_FieldExtractors.TryGetValue(i_TelemetryFieldKey, out Func<Vn310Telemetry, double>? extractor))
        {
            throw new ArgumentException($"Unknown VN310 telemetry field key '{i_TelemetryFieldKey}'.", nameof(i_TelemetryFieldKey));
        }

        SourceDevice = i_Device;
        m_Device = i_Device;
        m_Extractor = extractor;
        m_Device.TelemetryUpdated += OnTelemetryUpdated;
    }
    #endregion

    #region Functions
    // i_StepScale ignored: VN310 values are absolute, not deltas. Runs on UI thread (250ms timer)
    public override void Tick(double i_StepScale)
    {
        Value = Volatile.Read(ref m_LatestValue);
    }

    // 100Hz background recording loop reads this directly for the freshest value, bypassing UI cadence
    public override double GetSnapshotValue() => Volatile.Read(ref m_LatestValue);
    #endregion

    #region Event Handlers
    // Fires on the VN310 SDK's read thread. No PropertyChanged here -- UI binding would throw cross-thread
    private void OnTelemetryUpdated(object? i_Sender, Vn310Telemetry i_Telemetry)
    {
        Volatile.Write(ref m_LatestValue, m_Extractor(i_Telemetry));
    }
    #endregion

    #region IDisposable
    public void Dispose()
    {
        if (m_Disposed) { return; }
        m_Device.TelemetryUpdated -= OnTelemetryUpdated;
        m_Disposed = true;
    }
    #endregion
}
