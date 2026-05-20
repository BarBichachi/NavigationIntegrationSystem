using System;

namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// Immutable snapshot of one parsed VN310 packet's worth of telemetry. Field units: degrees for angles + lat/lon, meters for altitude, m/s for velocity + speed, deg/s for angular rates. YawDeg is pre-wrapped to [0, 360). Yaw/Pitch/Roll rates are 0 when the source was ASCII VNINS (HasRates == false).
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

    public double YawRateDegS { get; init; }
    public double PitchRateDegS { get; init; }
    public double RollRateDegS { get; init; }

    public double VelNorth { get; init; }
    public double VelEast { get; init; }
    public double VelDown { get; init; }
    public double Speed { get; init; }

    public float AttUncertainty { get; init; }
    public float PosUncertainty { get; init; }
    public float VelUncertainty { get; init; }

    public Vn310InsStatus InsStatus { get; init; } = new Vn310InsStatus();
    public Vn310TimeStatus TimeStatus { get; init; } = new Vn310TimeStatus();

    // True when the source packet was binary and included the AngularRate group; false when the source was ASCII VNINS (rates default to 0)
    public bool HasRates { get; init; }

    public DateTime PacketReceivedAt { get; init; }
    #endregion
}
