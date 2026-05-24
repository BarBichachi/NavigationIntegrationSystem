using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

namespace NavigationIntegrationSystem.UI.Views.Panes;

// Picks the matching DataTemplate per concrete DeviceSettingsPaneViewModelBase subclass. Used by DeviceSettingsPaneView's ContentControl so each device type renders its own bespoke sub-view -- no Visibility-toggled visual-tree-stamping of every sub-view (which was the source of cross-pane binding leaks before this refactor)
public sealed class SettingsPaneTemplateSelector : DataTemplateSelector
{
    #region Properties
    // Assigned in XAML; one per supported device-type pane VM
    public DataTemplate? RealDeviceTemplate { get; set; }
    public DataTemplate? Vn310Template { get; set; }
    public DataTemplate? PlaybackTemplate { get; set; }
    #endregion

    #region Functions
    protected override DataTemplate SelectTemplateCore(object i_Item, DependencyObject i_Container)
    {
        return Pick(i_Item) ?? RealDeviceTemplate!;
    }

    protected override DataTemplate SelectTemplateCore(object i_Item)
    {
        return Pick(i_Item) ?? RealDeviceTemplate!;
    }

    private DataTemplate? Pick(object? i_Item)
    {
        return i_Item switch
        {
            Vn310SettingsPaneViewModel => Vn310Template,
            PlaybackSettingsPaneViewModel => PlaybackTemplate,
            RealDeviceSettingsPaneViewModel => RealDeviceTemplate,
            _ => null
        };
    }
    #endregion
}
