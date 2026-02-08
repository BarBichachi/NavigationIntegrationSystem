using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NavigationIntegrationSystem.Core.Recording;

namespace NavigationIntegrationSystem.UI.ViewModels;

// Owns top-level UI state for the app shell, including recording controls
public sealed partial class MainViewModel : ObservableObject
{
    #region Private Fields
    private readonly IRecordingService m_RecordingService;
    private bool m_IsRecording;
    #endregion

    #region Properties
    // Indicates if the system is currently recording data
    public bool IsRecording { get => m_IsRecording; private set => SetProperty(ref m_IsRecording, value); }
    #endregion

    #region Commands
    // Toggles the recording state
    public IRelayCommand ToggleRecordingCommand { get; }
    #endregion

    #region Constructors
    public MainViewModel(IRecordingService i_RecordingService)
    {
        m_RecordingService = i_RecordingService;
        m_IsRecording = m_RecordingService.IsRecording;

        ToggleRecordingCommand = new RelayCommand(OnToggleRecording);

        m_RecordingService.RecordingStateChanged += OnRecordingStateChanged;
    }
    #endregion

    #region Functions
    // Handles the UI request to start or stop recording
    private void OnToggleRecording()
    {
        if (m_RecordingService.IsRecording)
        {
            m_RecordingService.Stop();
        }
        else
        {
            m_RecordingService.Start();
        }
    }
    #endregion

    #region Event Handlers
    // Synchronizes the local property with the service state
    private void OnRecordingStateChanged(object? sender, bool i_IsRecording)
    {
        IsRecording = i_IsRecording;
    }
    #endregion
}