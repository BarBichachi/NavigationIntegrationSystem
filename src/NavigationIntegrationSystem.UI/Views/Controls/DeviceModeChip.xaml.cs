using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.Core.Devices;

namespace NavigationIntegrationSystem.UI.Views.Controls;

// Small reusable colored pill showing a device's self-reported mode (e.g. VN310 TRACKING / ALIGNING / ...). Two flat DPs (Label + Severity) instead of a single Snapshot DP because x:Bind in WinUI 3 lacks null-safe property navigation -- callers expose flattened props on their VM and bind primitives, which keeps null handling at the binding boundary instead of inside this control
public sealed partial class DeviceModeChip : UserControl
{
    #region Dependency Properties
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(DeviceModeChip),
        new PropertyMetadata(null));

    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity),
        typeof(int),
        typeof(DeviceModeChip),
        new PropertyMetadata((int)DeviceModeSeverity.Unknown));
    #endregion

    #region Properties
    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public DeviceModeSeverity Severity
    {
        get => (DeviceModeSeverity)(int)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, (int)value);
    }
    #endregion

    #region Constructors
    public DeviceModeChip()
    {
        InitializeComponent();
    }
    #endregion
}
