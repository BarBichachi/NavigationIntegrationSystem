using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

// Settings pane for hardware-backed devices that use the generic Connection-kind selector + UDP/TCP/Serial subsections (TMAPS today). Future devices that need bespoke fields should create their own subclass instead of bloating this one
public sealed partial class RealDeviceSettingsPaneViewModel : DeviceSettingsPaneViewModelBase
{
    #region Constructors
    public RealDeviceSettingsPaneViewModel(DevicesViewModel i_Parent, DeviceCardViewModel i_Device, IDialogService i_DialogService)
        : base(i_Parent, i_Device, i_DialogService)
    {
    }
    #endregion
}
