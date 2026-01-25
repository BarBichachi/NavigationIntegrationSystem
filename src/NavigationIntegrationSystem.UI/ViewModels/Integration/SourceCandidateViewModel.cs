using CommunityToolkit.Mvvm.ComponentModel;
using NavigationIntegrationSystem.Core.Enums;
using System;
using System.ComponentModel;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Represents one source candidate value (per device) and provides display text for the selection UI
public sealed partial class SourceCandidateViewModel : ObservableObject
{
    #region Private Fields
    private readonly Random m_Rng;
    private double m_CandidateValue;
    private bool m_IsSelected;
    #endregion

    #region Properties
    public DeviceType DeviceType { get; }
    public string DisplayName { get; }
    public string DisplayText => $"{CandidateValue:0.00000}";
    public double CandidateValue { get => m_CandidateValue; set { if (SetProperty(ref m_CandidateValue, value)) { OnPropertyChanged(nameof(DisplayText)); } } }
    public IntegrationFieldRowViewModel Row { get; }
    public bool IsSelected { get => m_IsSelected; set { if (SetProperty(ref m_IsSelected, value) && value) { Row.UpdateSelection(this); } } }
    public bool IsManualEntry => DeviceType == DeviceType.Manual;
    #endregion

    #region Constructors
    public SourceCandidateViewModel(IntegrationFieldRowViewModel i_Row, DeviceType i_DeviceType, string i_DisplayName, double i_InitialValue, Random i_Rng)
    {
        Row = i_Row;
        DeviceType = i_DeviceType;
        DisplayName = i_DisplayName;
        m_Rng = i_Rng;
        m_CandidateValue = i_InitialValue;
    }
    #endregion

    #region Functions
    // Updates candidate value with a small random delta
    public void Tick(double i_StepScale)
    {
        if (IsManualEntry) return;

        double delta = (m_Rng.NextDouble() - 0.5) * i_StepScale;
        CandidateValue += delta;
    }
    #endregion
}