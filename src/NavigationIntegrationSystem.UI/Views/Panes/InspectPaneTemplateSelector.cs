using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

namespace NavigationIntegrationSystem.UI.Views.Panes;

// Picks the matching DataTemplate per concrete DeviceInspectPaneViewModelBase subclass. Used by DeviceInspectPaneView's ContentControl so each device type renders its own bespoke sub-view -- mirrors SettingsPaneTemplateSelector exactly. Adding a new device's bespoke inspect view = add a DataTemplate property here + a case in Pick()
public sealed class InspectPaneTemplateSelector : DataTemplateSelector
{
    #region Properties
    public DataTemplate? GenericTemplate { get; set; }
    public DataTemplate? Vn310Template { get; set; }
    #endregion

    #region Functions
    protected override DataTemplate SelectTemplateCore(object i_Item, DependencyObject i_Container)
    {
        return Pick(i_Item) ?? GenericTemplate!;
    }

    protected override DataTemplate SelectTemplateCore(object i_Item)
    {
        return Pick(i_Item) ?? GenericTemplate!;
    }

    private DataTemplate? Pick(object? i_Item)
    {
        return i_Item switch
        {
            Vn310InspectPaneViewModel => Vn310Template,
            GenericInspectPaneViewModel => GenericTemplate,
            _ => null
        };
    }
    #endregion
}
