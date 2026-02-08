using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

using System;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// Candidate backed by a numeric value (dummy telemetry for now)
public sealed partial class NumericSourceCandidateViewModel : IntegrationSourceCandidateViewModel
{
    #region Private Fields
    private readonly Random m_Rng;
    private double m_Value;
    #endregion

    #region Properties
    public double Value
    {
        get => m_Value;
        set { if (!SetProperty(ref m_Value, value)) { return; } OnPropertyChanged(nameof(DisplayText)); }
    }

    public override string DisplayText => $"{Value:0.00000}";
    #endregion

    #region Ctors
    public NumericSourceCandidateViewModel(IInsDevice i_Device, string i_DisplayName, double i_InitialValue, Random i_Rng)
        : base(i_Device.Definition.Type, i_DisplayName)
    {
        SourceDevice = i_Device;
        m_Value = i_InitialValue;
        m_Rng = i_Rng;
    }
    #endregion

    #region Functions
    // Updates the value with a small random delta (dummy telemetry)
    public override void Tick(double i_StepScale)
    {
        double delta = (m_Rng.NextDouble() - 0.5) * i_StepScale;
        Value += delta;
    }
    #endregion
}
