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
    private readonly SourceCandidateViewModel m_ManualSource;
    private readonly IntegrationViewModel m_Parent;
    #endregion

    #region Properties
    public string FieldName { get; }
    public string Unit { get; }
    public SourceCandidateViewModel ManualSource => m_ManualSource;
    public ObservableCollection<SourceCandidateViewModel> Sources { get; }
    public ObservableCollection<SourceCandidateViewModel> VisibleSources { get; } = new();
    public bool IsManualVisible { get => m_Parent.IsManualVisible; }
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

    public string SelectedValueText
    {
        get
        {
            if (m_SelectedSource == m_ManualSource && !IsManualVisible) { return "—"; }
            if (m_SelectedSource != m_ManualSource && !VisibleSources.Contains(m_SelectedSource)) { return "—"; }
            return m_SelectedSource?.DisplayText ?? "—";
        }
    }
    #endregion

    #region Constructors
    public IntegrationFieldRowViewModel(IntegrationViewModel i_Parent, string i_FieldName, string i_Unit, Random i_Rng)
    {
        m_Parent = i_Parent;
        FieldName = i_FieldName;
        Unit = i_Unit;
        Sources = new ObservableCollection<SourceCandidateViewModel>();

        // Initialize fixed manual source
        m_ManualSource = new SourceCandidateViewModel(this, DeviceType.Manual, "Manual", 0.0, i_Rng);

        // Default to Manual on creation
        SelectedSource = m_ManualSource;
        m_ManualSource.IsSelected = true;
    }
    #endregion

    #region Functions

    // Synchronizes selection state across the fixed manual source and dynamic device sources
    public void UpdateSelection(SourceCandidateViewModel i_Selected)
    {
        if (i_Selected == m_ManualSource) { DeselectAllDeviceSources(); }
        else if (m_ManualSource.IsSelected) { m_ManualSource.IsSelected = false; }

        SelectedSource = i_Selected;
    }

    // Ensures no dynamic device source remains selected
    private void DeselectAllDeviceSources()
    {
        foreach (SourceCandidateViewModel src in VisibleSources)
        {
            if (src.IsSelected) { src.IsSelected = false; }
        }
    }

    // Rebuilds the VisibleSources collection based on the provided visibility function
    public void RefreshVisibleSources(Func<SourceCandidateViewModel, bool> i_IsVisible)
    {
        VisibleSources.Clear();

        foreach (SourceCandidateViewModel src in Sources)
        {
            if (i_IsVisible(src))
            {
                if (src != m_SelectedSource)
                {
                    src.NotifySelectionChanged(false);
                }

                VisibleSources.Add(src);
            }
        }
    }

    // Automatically selects the first available visible source if the current one is hidden
    public void HandleVisibilityFallback()
    {
        // If current source is still visible, do nothing
        if (IsSourceVisible(m_SelectedSource)) { return; }

        // Fallback order: 1. First visible device, 2. Manual (if visible), 3. Null
        SourceCandidateViewModel? fallback = VisibleSources.FirstOrDefault() ?? (IsManualVisible ? m_ManualSource : null);

        if (fallback != null)
        {
            fallback.ForceSelect();
        }
        else
        {
            SelectedSource = null;
        }
    }

    // Explicitly notifies the UI to refresh the selection dot for the active source
    public void ReassertVisualSelection()
    {
        if (m_SelectedSource == null) { return; }

        m_SelectedSource.NotifySelectionChanged(true);
    }

    // Determines if a specific source is currently visible in the UI
    private bool IsSourceVisible(SourceCandidateViewModel? i_Source)
    {
        if (i_Source == null) { return false; }
        if (i_Source == m_ManualSource) { return IsManualVisible; }
        return VisibleSources.Contains(i_Source);
    }

    // Forces the UI to re-evaluate the integrated output text
    public void RefreshIntegratedOutput() => OnPropertyChanged(nameof(SelectedValueText));

    // Notifies that the Manual source visibility may have changed
    internal void NotifyManualVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsManualVisible));
        RefreshIntegratedOutput();
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