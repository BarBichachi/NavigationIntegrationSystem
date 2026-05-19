using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// Selects the correct template for an integration source candidate.
// DeviceTemplate covers any read-only device-sourced candidate (Numeric, Playback, and future feeds);
// ManualTemplate is the only special case because it needs a TextBox for user input.
public sealed partial class IntegrationCandidateTemplateSelector : DataTemplateSelector
{
    #region Properties
    public DataTemplate? DeviceTemplate { get; set; }
    public DataTemplate? ManualTemplate { get; set; }
    #endregion

    #region Overrides
    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item switch
        {
            ManualSourceCandidateViewModel => ManualTemplate!,
            IntegrationSourceCandidateViewModel => DeviceTemplate!,
            _ => base.SelectTemplateCore(item)
        };
    }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
    #endregion
}
