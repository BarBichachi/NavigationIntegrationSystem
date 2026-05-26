using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// Constructs an IntegrationSourceCandidateViewModel for a single (device, integration-row) pair. One implementation per device type; the implementation owns any device-specific field-key maps internally so the integration grid wiring stays device-agnostic. Returns null when this device can't source the requested field (e.g. VN310 doesn't produce Course; Velocity Total is calculated, no source)
public interface IIntegrationCandidateFactory
{
    #region Properties
    DeviceType Type { get; }
    #endregion

    #region Functions
    IntegrationSourceCandidateViewModel? Create(IInsDevice i_Device, string i_DisplayName, string i_IntegrationFieldName);
    #endregion
}
