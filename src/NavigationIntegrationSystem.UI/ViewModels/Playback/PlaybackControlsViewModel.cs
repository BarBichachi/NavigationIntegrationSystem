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
    public string PlayPauseIcon => IsPlaying ? "Pause" : "Play";
    public string PlayPauseTooltip => IsPlaying ? "Pause" : "Play";
    public string ProgressText => $"{CurrentLineIndex} / {TotalLineCount}";

    // Slider binding (TwoWay)
    public int SeekValue
    {
        get => CurrentLineIndex;
        set
        {
            if (value != CurrentLineIndex)
            {
                m_PlaybackService.Seek(value);
                OnPropertyChanged();
            }
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

        UpdateVisibility();
    }
    #endregion

    #region Functions
    // Toggles playback state
    private void OnTogglePlay()
    {
        if (m_PlaybackService.IsPlaying) m_PlaybackService.Pause();
        else m_PlaybackService.Play();
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

    // When playback state changes, update all relevant properties and visibility
    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        // Marshal to UI thread because this event comes from the playback loop
        m_DispatcherQueue.TryEnqueue(() =>
        {
            UpdateVisibility();
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(CurrentLineIndex));
            OnPropertyChanged(nameof(SeekValue));
            OnPropertyChanged(nameof(TotalLineCount));
            OnPropertyChanged(nameof(PlayPauseIcon));
            OnPropertyChanged(nameof(PlayPauseTooltip));
            OnPropertyChanged(nameof(ProgressText));
        });
    }
    #endregion
}