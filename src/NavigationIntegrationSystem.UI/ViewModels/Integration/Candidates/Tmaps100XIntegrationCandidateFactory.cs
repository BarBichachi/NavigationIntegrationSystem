using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Integration;

using System;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// TMAPS100X integration candidate factory. TMAPS100 telemetry is not yet wired up (Phase TBD for that device); this factory produces NumericSourceCandidateViewModel instances with dummy initial values offset from the canonical base so TMAPS rows look visually distinct from other devices on the same row. Replace the body of Create() with the real candidate once TMAPS100 telemetry comes online
public sealed class Tmaps100XIntegrationCandidateFactory : IIntegrationCandidateFactory
{
    #region Properties
    public DeviceType Type => DeviceType.Tmaps100X;
    #endregion

    #region Private Fields
    // Dummy initial-value offset so TMAPS dummy data is visually distinguishable from other dummy sources on the same integration row. Remove when real telemetry replaces the dummy path
    private const double c_DummyOffset = 5.0;

    private readonly Random m_Rng = new Random();
    #endregion

    #region Functions
    public IntegrationSourceCandidateViewModel? Create(IInsDevice i_Device, string i_DisplayName, string i_IntegrationFieldName)
    {
        double initial = ComputeDummyInitial(i_IntegrationFieldName) + c_DummyOffset;
        return new NumericSourceCandidateViewModel(i_Device, i_DisplayName, initial, m_Rng);
    }

    // Canonical base value per integration field for dummy telemetry. Real-telemetry factories don't use this -- it only seeds NumericSourceCandidateViewModel so dummy data looks like plausible-looking values rather than zeros
    private static double ComputeDummyInitial(string i_FieldName)
    {
        return i_FieldName switch
        {
            IntegrationFieldNames.Yaw       => 8.0,
            IntegrationFieldNames.Latitude  => 32.0853,
            IntegrationFieldNames.Longitude => 34.7818,
            IntegrationFieldNames.Altitude  => 120.5,
            IntegrationFieldNames.Pitch     => 0.2,
            IntegrationFieldNames.Roll      => -0.1,
            _                               => 0.0
        };
    }
    #endregion
}
