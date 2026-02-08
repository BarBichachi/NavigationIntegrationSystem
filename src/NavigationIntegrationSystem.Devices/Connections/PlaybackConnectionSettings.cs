namespace NavigationIntegrationSystem.Devices.Connections;

// Playback (File) connection parameters
public sealed class PlaybackConnectionSettings
{
    #region Properties
    public string FilePath { get; set; } = string.Empty;
    public bool Loop { get; set; } = false;
    #endregion

    #region Functions
    // Copies values from another instance
    public void CopyFrom(PlaybackConnectionSettings i_Source)
    {
        if (i_Source == null) { return; }
        FilePath = i_Source.FilePath;
        Loop = i_Source.Loop;
    }
    #endregion
}