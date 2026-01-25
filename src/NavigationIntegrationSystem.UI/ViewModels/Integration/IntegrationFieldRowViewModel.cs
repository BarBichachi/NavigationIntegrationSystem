using CommunityToolkit.Mvvm.ComponentModel;

using NavigationIntegrationSystem.Core.Enums;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Represents a single fused field row, exposing candidate sources and the currently selected output value
public sealed partial class IntegrationFieldRowViewModel : ObservableObject
{
    #region Private Fields
    private SourceCandidateViewModel? m_SelectedSource;
    private string m_IntegratedOutputDisplay = "—";
    #endregion

    #region Properties
    public string FieldName { get; }
    public string Unit { get; }
    public ObservableCollection<SourceCandidateViewModel> Sources { get; }
    public ObservableCollection<SourceCandidateViewModel> VisibleSources { get; } = new();
    public string IntegratedOutputDisplay
    {
        get => m_IntegratedOutputDisplay;
        private set => SetProperty(ref m_IntegratedOutputDisplay, value);
    }
    public SourceCandidateViewModel? SelectedSource
    {
        get => m_SelectedSource;
        set
        {
            if (m_SelectedSource != null) { m_SelectedSource.PropertyChanged -= OnSelectedSourceChanged; }
            m_SelectedSource = value;
            if (m_SelectedSource != null) { m_SelectedSource.PropertyChanged += OnSelectedSourceChanged; }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedValueText));
        }
    }

    public string SelectedValueText => SelectedSource == null ? "—" : $"{SelectedSource.DisplayText}";
    #endregion

    #region Events
    public event EventHandler SourceChanged;
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

    // Updates selection state when a new source is selected
    public void UpdateSelection(SourceCandidateViewModel selected)
    {
        foreach (var src in VisibleSources)
        {
            if (src != selected) src.IsSelected = false;
        }
        SelectedSource = selected;
    }

    // Rebuilds the VisibleSources collection based on the provided visibility function
    public void RefreshVisibleSources(Func<SourceCandidateViewModel, bool> i_IsVisible)
    {
        DeviceType? previousType = SelectedSource?.DeviceType;

        VisibleSources.Clear();

        // Filter and add visible sources
        foreach (SourceCandidateViewModel src in Sources)
        {
            if (i_IsVisible(src))
            {
                VisibleSources.Add(src);
                // Ensure IsSelected starts as false for all new additions
                src.IsSelected = false;
            }
        }

        // Find the best candidate to select
        SourceCandidateViewModel? nextToSelect = VisibleSources.FirstOrDefault(s => s.DeviceType == previousType)
                                               ?? VisibleSources.FirstOrDefault();

        // Use UpdateSelection to ensure only ONE is selected and the output is updated
        if (nextToSelect != null)
        {
            nextToSelect.IsSelected = true;
            UpdateSelection(nextToSelect);
        }
        else
        {
            SelectedSource = null;
        }
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