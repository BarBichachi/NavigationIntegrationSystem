using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavigationIntegrationSystem.UI.Views.Controls;

// Small reusable hint stamped above connection-setting inputs (e.g. "Recommended: 115200"). Single Text dependency property; binds via x:Bind on the consuming view to its VM's hint accessor
public sealed partial class RecommendedHint : UserControl
{
    #region Dependency Properties
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(RecommendedHint),
        new PropertyMetadata(null));
    #endregion

    #region Properties
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    #endregion

    #region Constructors
    public RecommendedHint()
    {
        InitializeComponent();
    }
    #endregion
}
