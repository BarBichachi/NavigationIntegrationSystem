using System.Collections.Generic;

namespace NavigationIntegrationSystem.Core.Playback;

// A decoupled data packet representing one row of simulation data
public sealed class PlaybackPacket
{
    #region Properties
    public IReadOnlyDictionary<string, double> Values { get; }
    #endregion

    #region Constructors
    public PlaybackPacket(IReadOnlyDictionary<string, double> i_Values)
    {
        Values = i_Values;
    }
    #endregion
}