using System;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Core.Playback;

// A decoupled data packet representing one row of simulation data
public sealed class PlaybackPacket
{
    #region Properties
    public DateTime Timestamp { get; }
    public IReadOnlyDictionary<string, double> Values { get; }
    #endregion

    #region Constructors
    public PlaybackPacket(DateTime i_Timestamp, IReadOnlyDictionary<string, double> i_Values)
    {
        Timestamp = i_Timestamp;
        Values = i_Values;
    }
    #endregion
}