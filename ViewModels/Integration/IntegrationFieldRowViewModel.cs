using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Represents a single fused field row, exposing candidate sources and the currently selected output value
public sealed partial class IntegrationFieldRowViewModel : ObservableObject
{
    #region Private Fields
    private SourceCandidateViewModel? m_SelectedSource;
    #endregion

    #region Properties
    public string FieldName { get; }
    public string Unit { get; }
    public ObservableCollection<SourceCandidateViewModel> Sources { get; }
    public ObservableCollection<SourceCandidateViewModel> VisibleSources { get; } = new ObservableCollection<SourceCandidateViewModel>();

    public SourceCandidateViewModel? SelectedSource
    {
        get => m_SelectedSource;
        set
        {
            if (m_SelectedSource == value) { return; }

            if (m_SelectedSource != null) { m_SelectedSource.PropertyChanged -= OnSelectedSourceChanged; }

            m_SelectedSource = value;
            OnPropertyChanged();

            if (m_SelectedSource != null) { m_SelectedSource.PropertyChanged += OnSelectedSourceChanged; }

            OnPropertyChanged(nameof(SelectedValueText));
        }
    }

    public string SelectedValueText => SelectedSource == null ? "—" : $"{SelectedSource.CandidateValue:0.00000}";
    #endregion

    #region Constructors
    public IntegrationFieldRowViewModel(string i_FieldName, string i_Unit, ObservableCollection<SourceCandidateViewModel> i_Sources)
    {
        FieldName = i_FieldName;
        Unit = i_Unit;
        Sources = i_Sources;
        SelectedSource = Sources.Count > 0 ? Sources[0] : null;
        foreach (SourceCandidateViewModel src in Sources) { VisibleSources.Add(src); }
    }
    #endregion

    #region Functions
    // Refreshes the connected candidate list based on a predicate and keeps SelectedSource valid
    public void RefreshVisibleSources(Func<SourceCandidateViewModel, bool> i_IsVisible)
    {
        VisibleSources.Clear();

        foreach (SourceCandidateViewModel src in Sources)
        {
            if (i_IsVisible(src)) { VisibleSources.Add(src); }
        }

        if (SelectedSource != null && VisibleSources.Contains(SelectedSource)) { return; }

        SelectedSource = VisibleSources.Count > 0 ? VisibleSources[0] : null;
    }
    #endregion

    #region Event Handlers
    // Keeps Value column live when the selected candidate changes
    private void OnSelectedSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SourceCandidateViewModel.CandidateValue))
        {
            OnPropertyChanged(nameof(SelectedValueText));
        }
    }
    #endregion
}