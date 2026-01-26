using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Base;
using System;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Represents a single candidate value coming from a device
public sealed partial class SourceCandidateViewModel : ViewModelBase
{
    #region Private Fields
    private readonly Random m_Rng;
    private double m_Value;
    private bool m_IsSelected;
    #endregion

    #region Properties
    public DeviceType DeviceType { get; }
    public string DisplayName { get; }
    public double Value
    {
        get => m_Value;
        set { if (!SetProperty(ref m_Value, value)) { return; } OnPropertyChanged(nameof(DisplayText)); }
    }
    public string DisplayText => $"{Value:0.00000}";
    public bool IsSelected { get => m_IsSelected; private set => SetProperty(ref m_IsSelected, value); }
    #endregion

    #region Constructors
    public SourceCandidateViewModel(DeviceType i_DeviceType, string i_DisplayName, double i_InitialValue, Random i_Rng)
    {
        DeviceType = i_DeviceType;
        DisplayName = i_DisplayName;
        m_Value = i_InitialValue;
        m_Rng = i_Rng;
    }
    #endregion

    #region Functions
    // Sets selection state (owned by the row)
    internal void SetSelected(bool i_IsSelected) { IsSelected = i_IsSelected; }

    // Updates the value with a small random delta (dummy telemetry)
    public void Tick(double i_StepScale)
    {
        double delta = (m_Rng.NextDouble() - 0.5) * i_StepScale;
        Value += delta;
    }
    #endregion
}
