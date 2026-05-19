using System.Collections.Generic;

namespace NavigationIntegrationSystem.Core.Playback;

// Single source of truth for the playback frequencies the settings UI offers and the
// devices-config import validator accepts. Keep these aligned; adding a frequency in one
// place but not the other would let a config import smuggle in a value the UI can't display.
public static class PlaybackFrequencies
{
    #region Properties
    public static IReadOnlyList<int> All { get; } = new[] { 1, 5, 10, 25, 50, 100 };
    #endregion
}
