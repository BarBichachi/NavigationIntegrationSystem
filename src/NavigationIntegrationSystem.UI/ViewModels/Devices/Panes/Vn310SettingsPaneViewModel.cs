using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

// Settings pane for VN310. Serial-only by design (no Connection-kind selector in the view) and exposes RecommendedHint accessors for the COM Port and Baud Rate inputs
public sealed partial class Vn310SettingsPaneViewModel : DeviceSettingsPaneViewModelBase
{
    #region Properties
    // Null-coalesce to empty so the RecommendedHint control collapses cleanly via StringNullOrEmptyToVisibilityConverter when the device definition leaves the hint unset
    public string BaudRateHintText => Device.Device.Definition.RecommendedConnection?.BaudRateHint ?? string.Empty;
    public string ComPortHintText => Device.Device.Definition.RecommendedConnection?.ComPortHint ?? string.Empty;
    #endregion

    #region Constructors
    public Vn310SettingsPaneViewModel(DevicesViewModel i_Parent, DeviceCardViewModel i_Device, IDialogService i_DialogService)
        : base(i_Parent, i_Device, i_DialogService)
    {
    }
    #endregion
}
