using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Selects the correct template for an integration source candidate
public sealed class IntegrationCandidateTemplateSelector : DataTemplateSelector
{
    #region Properties
    public DataTemplate? NumericTemplate { get; set; }
    public DataTemplate? ManualTemplate { get; set; }
    #endregion

    #region Overrides
    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item switch
        {
            ManualSourceCandidateViewModel => ManualTemplate!,
            NumericSourceCandidateViewModel => NumericTemplate!,
            _ => base.SelectTemplateCore(item)
        };
    }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
    #endregion
}