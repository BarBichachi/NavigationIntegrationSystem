using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Playback;
using System;
using System.Threading;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// Candidate backed by the live Playback packet stream for a single CSV column.
// Packets arrive on the playback background thread; we write the latest value via Volatile.Write and
// deliberately do NOT fire PropertyChanged from there (WinUI bindings dislike cross-thread notifications).
// UI Tick (250ms timer) copies the latest value into the observable Value, firing PropertyChanged on UI.
// The 100Hz recording loop calls GetSnapshotValue() directly for the freshest read, bypassing UI cadence.
public sealed class PlaybackSourceCandidateViewModel : IntegrationSourceCandidateViewModel, IDisposable
{
    #region Private Fields
    private readonly IPlaybackService m_PlaybackService;
    private readonly string m_CsvKey;
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
    public PlaybackSourceCandidateViewModel(IInsDevice i_Device, string i_DisplayName, string i_CsvKey, IPlaybackService i_PlaybackService)
        : base(i_Device.Definition.Type, i_DisplayName)
    {
        SourceDevice = i_Device;
        m_CsvKey = i_CsvKey;
        m_PlaybackService = i_PlaybackService;
        m_PlaybackService.PacketDispatched += OnPacketDispatched;
    }
    #endregion

    #region Functions
    // i_StepScale ignored: playback values are absolute, not deltas. Runs on UI thread (250ms timer).
    public override void Tick(double i_StepScale)
    {
        Value = Volatile.Read(ref m_LatestValue);
    }

    // 100Hz background recording loop reads this directly for the freshest value, bypassing UI cadence.
    public override double GetSnapshotValue() => Volatile.Read(ref m_LatestValue);
    #endregion

    #region Event Handlers
    // Background callback from the playback service. No PropertyChanged here — UI binding would throw cross-thread.
    private void OnPacketDispatched(object? sender, PlaybackPacket e)
    {
        if (e.Values.TryGetValue(m_CsvKey, out double value))
        {
            Volatile.Write(ref m_LatestValue, value);
        }
    }
    #endregion

    #region IDisposable
    public void Dispose()
    {
        if (m_Disposed) { return; }
        m_PlaybackService.PacketDispatched -= OnPacketDispatched;
        m_Disposed = true;
    }
    #endregion
}
