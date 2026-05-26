using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Integration;
using NavigationIntegrationSystem.Devices.Implementations.Vn310;

using System.Collections.Generic;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// VN310 integration candidate factory. Owns the field-name → Vn310Telemetry-property-name map (kept private since it's an implementation detail of how VN310 surfaces its telemetry to the integration grid). Returns null for fields the sensor doesn't produce (e.g. Course is omitted because VN310 does not output it directly; Velocity Total is calculated and has no source)
public sealed class Vn310IntegrationCandidateFactory : IIntegrationCandidateFactory
{
    #region Properties
    public DeviceType Type => DeviceType.VN310;
    #endregion

    #region Private Fields
    private static readonly IReadOnlyDictionary<string, string> s_FieldToTelemetryKey = new Dictionary<string, string>
    {
        [IntegrationFieldNames.Latitude]      = "LatDeg",
        [IntegrationFieldNames.Longitude]     = "LonDeg",
        [IntegrationFieldNames.Altitude]      = "AltM",

        [IntegrationFieldNames.Yaw]           = "YawDeg",
        [IntegrationFieldNames.Pitch]         = "PitchDeg",
        [IntegrationFieldNames.Roll]          = "RollDeg",

        [IntegrationFieldNames.YawRate]       = "YawRateDegS",
        [IntegrationFieldNames.PitchRate]     = "PitchRateDegS",
        [IntegrationFieldNames.RollRate]      = "RollRateDegS",

        [IntegrationFieldNames.VelocityNorth] = "VelNorth",
        [IntegrationFieldNames.VelocityEast]  = "VelEast",
        [IntegrationFieldNames.VelocityDown]  = "VelDown",
    };
    #endregion

    #region Functions
    public IntegrationSourceCandidateViewModel? Create(IInsDevice i_Device, string i_DisplayName, string i_IntegrationFieldName)
    {
        if (!s_FieldToTelemetryKey.TryGetValue(i_IntegrationFieldName, out string? telemetryKey)) { return null; }
        return new Vn310SourceCandidateViewModel((Vn310InsDevice)i_Device, i_DisplayName, telemetryKey);
    }
    #endregion
}
