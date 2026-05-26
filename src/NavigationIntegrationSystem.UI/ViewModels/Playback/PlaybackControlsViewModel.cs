using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using NavigationIntegrationSystem.Core.Playback;
using NavigationIntegrationSystem.UI.ViewModels.Base;
using System;

namespace NavigationIntegrationSystem.UI.ViewModels.Playback;

// Manages the state of the bottom playback tray
public sealed partial class PlaybackControlsViewModel : ViewModelBase
{
    #region Private Fields
    private readonly IPlaybackService m_PlaybackService;
    private readonly DispatcherQueue m_DispatcherQueue;
    private bool m_IsVisible;
    #endregion

    #region Properties
    public bool IsVisible { get => m_IsVisible; private set => SetProperty(ref m_IsVisible, value); }
    public bool IsPlaying => m_PlaybackService.IsPlaying;
    public int CurrentLineIndex => m_PlaybackService.CurrentLineIndex;
    public int TotalLineCount => m_PlaybackService.TotalLineCount;

    // True when playback has reached end-of-file (last line dispatched and the loop exited naturally).
    public bool IsAtEnd => TotalLineCount > 0 && CurrentLineIndex >= TotalLineCount;

    // Stop is only meaningful while playing OR while sitting partway through the file (not at start, not at end).
    public bool CanStop => IsPlaying || (CurrentLineIndex > 0 && !IsAtEnd);

    // Icon swaps to Refresh once we hit EOF so the user knows the same button now restarts from the beginning.
    public string PlayPauseIcon
    {
        get
        {
            if (IsPlaying) { return "Pause"; }
            if (IsAtEnd) { return "Refresh"; }
            return "Play";
        }
    }

    public string PlayPauseTooltip
    {
        get
        {
            if (IsPlaying) { return "Pause"; }
            if (IsAtEnd) { return "Restart"; }
            return "Play";
        }
    }

    public string ProgressText => $"{CurrentLineIndex} / {TotalLineCount}";

    // "hh:mm:ss / hh:mm:ss" - content-time elapsed vs total, derived from line index and current playback frequency.
    // Uses the live Frequency value, so a mid-playback frequency change is reflected on the next position tick.
    public string ElapsedTimeText
    {
        get
        {
            int freq = m_PlaybackService.Frequency;
            if (freq <= 0) { return "00:00:00 / 00:00:00"; }
            TimeSpan elapsed = TimeSpan.FromSeconds(CurrentLineIndex / (double)freq);
            TimeSpan total = TimeSpan.FromSeconds(TotalLineCount / (double)freq);
            return $"{elapsed:hh\\:mm\\:ss} / {total:hh\\:mm\\:ss}";
        }
    }

    // Slider binding (TwoWay)
    public int SeekValue
    {
        get => CurrentLineIndex;
        set
        {
            if (value == CurrentLineIndex) { return; }
            m_PlaybackService.Seek(value);
            // Seek fires PositionChanged on the service, which will propagate back via OnPlaybackPositionChanged.
            // Fire here too so the slider's own TwoWay binding doesn't lag a tick behind the text.
            RaiseAllPositionProperties();
        }
    }
    #endregion

    #region Commands
    public IRelayCommand TogglePlayCommand { get; }
    public IRelayCommand StopCommand { get; }
    #endregion

    #region Constructors
    public PlaybackControlsViewModel(IPlaybackService i_PlaybackService)
    {
        m_PlaybackService = i_PlaybackService;
        m_DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        TogglePlayCommand = new RelayCommand(OnTogglePlay);
        StopCommand = new RelayCommand(OnStop);

        m_PlaybackService.StateChanged += OnPlaybackStateChanged;
        m_PlaybackService.PositionChanged += OnPlaybackPositionChanged;

        UpdateVisibility();
    }
    #endregion

    #region Functions
    // Three states: playing → pause; paused mid-file → play; at EOF → seek-to-start + play.
    private void OnTogglePlay()
    {
        if (m_PlaybackService.IsPlaying)
        {
            m_PlaybackService.Pause();
            return;
        }

        if (IsAtEnd)
        {
            m_PlaybackService.Seek(0);
            m_PlaybackService.Play();
            return;
        }

        m_PlaybackService.Play();
    }

    // Stops playback and resets to the beginning
    private void OnStop()
    {
        m_PlaybackService.Stop();
    }

    // Updates visibility based on whether a file is loaded
    private void UpdateVisibility()
    {
        IsVisible = !string.IsNullOrEmpty(m_PlaybackService.LoadedFilePath);
    }

    // Fires PropertyChanged for every property that depends on CurrentLineIndex / TotalLineCount / IsPlaying.
    private void RaiseAllPositionProperties()
    {
        OnPropertyChanged(nameof(CurrentLineIndex));
        OnPropertyChanged(nameof(SeekValue));
        OnPropertyChanged(nameof(TotalLineCount));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ElapsedTimeText));
        OnPropertyChanged(nameof(IsAtEnd));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(PlayPauseIcon));
        OnPropertyChanged(nameof(PlayPauseTooltip));
    }
    #endregion

    #region Event Handlers
    // Marshals to UI thread because StateChanged fires from the playback loop (background) on EOF
    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        m_DispatcherQueue.TryEnqueue(() =>
        {
            UpdateVisibility();
            OnPropertyChanged(nameof(IsPlaying));
            RaiseAllPositionProperties();
        });
    }

    // Fires per line advance during playback (background) and on every Seek (caller's thread). Marshal to UI.
    private void OnPlaybackPositionChanged(object? sender, EventArgs e)
    {
        m_DispatcherQueue.TryEnqueue(RaiseAllPositionProperties);
    }
    #endregion
}
