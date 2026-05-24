using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using System.Collections.ObjectModel;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

// Placeholder inspect pane VM used by devices that don't yet have a bespoke inspect view (TMAPS today). Surfaces the static field list from DeviceDefinition.Fields -- values are never populated, so this is a structural placeholder. When a device gets its own bespoke pane VM + sub-view, the factory will stop returning this for it
public sealed partial class GenericInspectPaneViewModel : DeviceInspectPaneViewModelBase
{
    #region Properties
    public ObservableCollection<InspectFieldViewModel> Fields => Device.InspectFields;
    #endregion

    #region Constructors
    public GenericInspectPaneViewModel(DeviceCardViewModel i_Device) : base(i_Device)
    {
    }
    #endregion
}
