using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Integration;
using NavigationIntegrationSystem.Core.Playback;

using System.Collections.Generic;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// Playback integration candidate factory. Owns the field-name → CSV-column-key map; keys (EulerAzimuth*) are locked to the parent solution's recorder format (see Infrastructure/TO_BE_DELETED/IntegratedInsOutputItem.IntegratedInsOutputColumns), even though NIS now labels these rows "Yaw" instead of "Azimuth". Returns null for the calculated Velocity Total row (no source)
public sealed class PlaybackIntegrationCandidateFactory : IIntegrationCandidateFactory
{
    #region Properties
    public DeviceType Type => DeviceType.Playback;
    #endregion

    #region Private Fields
    private readonly IPlaybackService m_PlaybackService;

    private static readonly IReadOnlyDictionary<string, string> s_FieldToCsvKey = new Dictionary<string, string>
    {
        [IntegrationFieldNames.Latitude]      = "PositionLatValue",
        [IntegrationFieldNames.Longitude]     = "PositionLonValue",
        [IntegrationFieldNames.Altitude]      = "PositionAltValue",

        [IntegrationFieldNames.Yaw]           = "EulerAzimuthValue",
        [IntegrationFieldNames.Pitch]         = "EulerPitchValue",
        [IntegrationFieldNames.Roll]          = "EulerRollValue",

        [IntegrationFieldNames.YawRate]       = "EulerAzimuthRateValue",
        [IntegrationFieldNames.PitchRate]     = "EulerPitchRateValue",
        [IntegrationFieldNames.RollRate]      = "EulerRollRateValue",

        [IntegrationFieldNames.VelocityNorth] = "VelocityNorthValue",
        [IntegrationFieldNames.VelocityEast]  = "VelocityEastValue",
        [IntegrationFieldNames.VelocityDown]  = "VelocityDownValue",

        [IntegrationFieldNames.Course]        = "CourseValue",
    };
    #endregion

    #region Constructors
    public PlaybackIntegrationCandidateFactory(IPlaybackService i_PlaybackService)
    {
        m_PlaybackService = i_PlaybackService;
    }
    #endregion

    #region Functions
    public IntegrationSourceCandidateViewModel? Create(IInsDevice i_Device, string i_DisplayName, string i_IntegrationFieldName)
    {
        if (!s_FieldToCsvKey.TryGetValue(i_IntegrationFieldName, out string? csvKey)) { return null; }
        return new PlaybackSourceCandidateViewModel(i_Device, i_DisplayName, csvKey, m_PlaybackService);
    }
    #endregion
}
