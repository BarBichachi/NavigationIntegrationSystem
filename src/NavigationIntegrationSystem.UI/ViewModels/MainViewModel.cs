using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Dispatching;

using NavigationIntegrationSystem.Core.Recording;
using NavigationIntegrationSystem.UI.ViewModels.Base;

using System;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.ViewModels;

// Owns top-level UI state for the app shell, including recording controls
public sealed partial class MainViewModel : ViewModelBase
{
    #region Constants
    // 250ms feels smooth without being chatty for an HH:MM:SS readout that only updates per second.
    private const int c_ElapsedTimerIntervalMs = 250;
    #endregion

    #region Private Fields
    private readonly IRecordingService m_RecordingService;
    private readonly DispatcherQueue m_DispatcherQueue;
    private DispatcherQueueTimer? m_ElapsedTimer;
    private DateTime? m_RecordingStartedAt;
    private bool m_IsRecording;
    private bool m_IsDevicePaneOpen;
    #endregion

    #region Properties
    public bool IsRecording
    {
        get => m_IsRecording;
        private set
        {
            if (!SetProperty(ref m_IsRecording, value)) { return; }
            OnPropertyChanged(nameof(IsRecordingEnabled));

            // Drive the elapsed-time clock off the same state transition that flips the button text
            if (value)
            {
                m_RecordingStartedAt = DateTime.UtcNow;
                StartElapsedTimer();
            }
            else
            {
                StopElapsedTimer();
                m_RecordingStartedAt = null;
            }
            OnPropertyChanged(nameof(ElapsedRecordingTime));
        }
    }
    public bool IsDevicePaneOpen
    {
        get => m_IsDevicePaneOpen;
        set
        {
            if (SetProperty(ref m_IsDevicePaneOpen, value)) { OnPropertyChanged(nameof(IsRecordingEnabled)); }
        }
    }
    public bool IsRecordingEnabled => !IsDevicePaneOpen || IsRecording;

    // Wall-clock time elapsed since recording started, formatted "hh:mm:ss". Displays "00:00:00" when not recording.
    public string ElapsedRecordingTime
    {
        get
        {
            if (m_RecordingStartedAt == null) { return "00:00:00"; }
            TimeSpan elapsed = DateTime.UtcNow - m_RecordingStartedAt.Value;
            return elapsed.ToString(@"hh\:mm\:ss");
        }
    }
    #endregion

    #region Commands
    // Async so the file-open / file-close work on the underlying legacy recorder runs off the UI thread; AsyncRelayCommand also disables re-entry while in flight (prevents click queueing during a slow Start/Stop).
    public IAsyncRelayCommand ToggleRecordingCommand { get; }
    #endregion

    #region Constructors
    public MainViewModel(IRecordingService i_RecordingService)
    {
        m_RecordingService = i_RecordingService;
        m_IsRecording = m_RecordingService.IsRecording;
        // Captured at construction (App.OnLaunched runs on the UI thread, which is where DI builds the singletons)
        m_DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ToggleRecordingCommand = new AsyncRelayCommand(OnToggleRecordingAsync);

        m_RecordingService.RecordingStateChanged += OnRecordingStateChanged;
    }
    #endregion

    #region Functions
    // Optimistic-update pattern: flip IsRecording on the VM IMMEDIATELY so the button text/icon update without
    // waiting for the cross-thread RecordingStateChanged event to round-trip. The await reconciles with the actual
    // service state after Start/Stop returns - covers the case where Start() fails inside the legacy recorder and
    // the state event never fires.
    private async Task OnToggleRecordingAsync()
    {
        bool wasRecording = m_RecordingService.IsRecording;
        IsRecording = !wasRecording;

        await Task.Run(() =>
        {
            if (wasRecording) { m_RecordingService.Stop(); }
            else { m_RecordingService.Start(); }
        });

        IsRecording = m_RecordingService.IsRecording;
    }

    // Spins up a DispatcherQueueTimer on the UI thread; ticks fire OnPropertyChanged for the readout
    private void StartElapsedTimer()
    {
        if (m_ElapsedTimer != null) { return; }
        m_ElapsedTimer = m_DispatcherQueue.CreateTimer();
        m_ElapsedTimer.Interval = TimeSpan.FromMilliseconds(c_ElapsedTimerIntervalMs);
        m_ElapsedTimer.Tick += OnElapsedTimerTick;
        m_ElapsedTimer.Start();
    }

    private void StopElapsedTimer()
    {
        if (m_ElapsedTimer == null) { return; }
        m_ElapsedTimer.Stop();
        m_ElapsedTimer.Tick -= OnElapsedTimerTick;
        m_ElapsedTimer = null;
    }
    #endregion

    #region Event Handlers
    // RecordingStateChanged fires from whichever thread called Start/Stop - now the threadpool (see Task.Run above). Marshal to UI before mutating bound properties.
    private void OnRecordingStateChanged(object? sender, bool i_IsRecording)
    {
        if (m_DispatcherQueue.HasThreadAccess)
        {
            IsRecording = i_IsRecording;
        }
        else
        {
            m_DispatcherQueue.TryEnqueue(() => IsRecording = i_IsRecording);
        }
    }

    private void OnElapsedTimerTick(DispatcherQueueTimer i_Sender, object i_Args)
    {
        OnPropertyChanged(nameof(ElapsedRecordingTime));
    }
    #endregion
}
