using System;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Represents one source candidate value (per device) and provides display text for the selection UI
public sealed partial class SourceCandidateViewModel : ObservableObject
{
    #region Private Fields
    private readonly Random m_Rng;
    private double m_CandidateValue;
    #endregion

    #region Properties
    public string DeviceName { get; }
    public int SlotIndex { get; } // A/B/C mapping if you want it later
    public double CandidateValue { get => m_CandidateValue; set { if (SetProperty(ref m_CandidateValue, value)) { OnPropertyChanged(nameof(DisplayText)); } } }
    public string DisplayText => $"{DeviceName}: {CandidateValue:0.00000}";
    public IntegrationFieldRowViewModel Row { get; }
    public bool IsSelected
    {
        get => ReferenceEquals(Row.SelectedSource, this);
        set
        {
            if (!value) { return; }
            if (ReferenceEquals(Row.SelectedSource, this)) { return; }
            Row.SelectedSource = this;
        }
    }
    #endregion

    #region Constructors
    public SourceCandidateViewModel(IntegrationFieldRowViewModel i_Row, string i_DeviceName, int i_SlotIndex, double i_InitialValue, Random i_Rng)
    {
        Row = i_Row;
        Row.PropertyChanged += OnRowPropertyChanged;
        DeviceName = i_DeviceName;
        SlotIndex = i_SlotIndex;
        m_Rng = i_Rng;
        m_CandidateValue = i_InitialValue;
    }
    #endregion

    #region Functions
    // Updates candidate value with a small random delta
    public void Tick(double i_StepScale)
    {
        double delta = (m_Rng.NextDouble() - 0.5) * i_StepScale;
        CandidateValue = CandidateValue + delta;
    }
    #endregion

    #region Event Handlers
    // Updates IsSelected when the row's selection changes
    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IntegrationFieldRowViewModel.SelectedSource))
        {
            OnPropertyChanged(nameof(IsSelected));
        }
    }
    #endregion
}