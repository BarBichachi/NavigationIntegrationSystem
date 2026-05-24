using NavigationIntegrationSystem.UI.ViewModels.Base;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using System;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

// Common base for any device inspect pane VM. Owns the card reference and the disposal contract; subclasses add device-specific telemetry subscriptions, decoded-status sections, packet stats, etc. The pane host (DeviceInspectPaneView) binds to this base type and a DataTemplate selects the concrete sub-view per runtime subclass -- same pattern as DeviceSettingsPaneViewModelBase
public abstract partial class DeviceInspectPaneViewModelBase : ViewModelBase, IDisposable
{
    #region Properties
    public DeviceCardViewModel Device { get; }
    public string DisplayName => Device.DisplayName;
    #endregion

    #region Constructors
    protected DeviceInspectPaneViewModelBase(DeviceCardViewModel i_Device)
    {
        Device = i_Device;
    }
    #endregion

    #region Functions
    // Default no-op. Subclasses that subscribe to device events or own timers override to release them when the inspect pane closes
    public virtual void Dispose() { }
    #endregion
}
