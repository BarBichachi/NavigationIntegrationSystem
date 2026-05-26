using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// Manual integration candidate factory. Creates a textbox-backed candidate for every row -- no field-key map needed because the user enters any value they want. The IInsDevice argument is unused (Manual is a pseudo-device with no telemetry)
public sealed class ManualIntegrationCandidateFactory : IIntegrationCandidateFactory
{
    #region Properties
    public DeviceType Type => DeviceType.Manual;
    #endregion

    #region Functions
    public IntegrationSourceCandidateViewModel? Create(IInsDevice i_Device, string i_DisplayName, string i_IntegrationFieldName)
    {
        return new ManualSourceCandidateViewModel(i_DisplayName);
    }
    #endregion
}
