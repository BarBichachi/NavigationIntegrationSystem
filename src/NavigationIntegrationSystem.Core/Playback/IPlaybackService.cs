using System;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.Core.Playback;

// Contract for the simulation engine responsible for streaming file data
public interface IPlaybackService : IDisposable
{
    #region Properties
    bool IsPlaying { get; }
    string? LoadedFilePath { get; }
    int CurrentLineIndex { get; }
    int TotalLineCount { get; }
    #endregion

    #region Events
    // Fired when a new data row is processed (on a background thread)
    event EventHandler<PlaybackPacket>? PacketDispatched;

    // Fired when play/pause state or file load status changes
    event EventHandler? StateChanged;
    #endregion

    #region Functions
    // Loads and validates a CSV file
    Task LoadFileAsync(string i_FilePath);

    // Starts or resumes playback
    void Play();

    // Pauses playback at the current index
    void Pause();

    // Stops playback and resets index to 0
    void Stop();

    // Seeks to a specific line index
    void Seek(int i_LineIndex);
    #endregion
}