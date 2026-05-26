using System;

namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// Immutable per-tick snapshot of VN310 telemetry, composed from up to two concurrent sources (ASCII VNINS and Binary CommonGroup+TimeGroup). Shared fields (YPR, LLA, NED, InsStatus, UtcTime) reflect the freshest of the two sources; per-source-exclusive fields reflect the latest packet from that source if it is fresh, else default. Field units: degrees for angles + lat/lon, meters for altitude, m/s for velocity + speed, deg/s for angular rates. YawDeg is pre-wrapped to [0, 360)
public sealed class Vn310Telemetry
{
    #region Properties
    public DateTime UtcTime { get; init; }

    public double LatDeg { get; init; }
    public double LonDeg { get; init; }
    public double AltM { get; init; }

    public double YawDeg { get; init; }
    public double PitchDeg { get; init; }
    public double RollDeg { get; init; }

    // Binary-exclusive (CommonGroup.AngularRate). Zero when IsBinaryFresh is false
    public double YawRateDegS { get; init; }
    public double PitchRateDegS { get; init; }
    public double RollRateDegS { get; init; }

    public double VelNorth { get; init; }
    public double VelEast { get; init; }
    public double VelDown { get; init; }
    public double Speed { get; init; }

    // ASCII-exclusive (VNINS fields). Zero when IsAsciiFresh is false
    public float AttUncertainty { get; init; }
    public float PosUncertainty { get; init; }
    public float VelUncertainty { get; init; }

    public Vn310InsStatus InsStatus { get; init; } = new Vn310InsStatus();

    // Binary-exclusive (TimeGroup.TimeStatus). Default when IsBinaryFresh is false
    public Vn310TimeStatus TimeStatus { get; init; } = new Vn310TimeStatus();

    // True when a binary packet was received within the staleness window at the moment this snapshot was published. Drives whether rates / TimeStatus fields carry real values
    public bool IsBinaryFresh { get; init; }

    // True when an ASCII packet was received within the staleness window at the moment this snapshot was published. Drives whether uncertainty fields carry real values
    public bool IsAsciiFresh { get; init; }

    public DateTime PacketReceivedAt { get; init; }
    #endregion
}
