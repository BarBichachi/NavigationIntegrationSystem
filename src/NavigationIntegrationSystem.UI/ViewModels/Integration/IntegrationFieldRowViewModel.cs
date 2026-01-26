using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Represents a single integration field row (parameter + selectable sources)
public sealed partial class IntegrationFieldRowViewModel : ViewModelBase
{
    #region Private Fields
    private IntegrationSourceCandidateViewModel? m_SelectedSource;
    #endregion

    #region Properties
    public string FieldName { get; }
    public string Unit { get; }

    public ObservableCollection<IntegrationSourceCandidateViewModel> Sources { get; } = [];
    public ObservableCollection<IntegrationSourceCandidateViewModel> VisibleSources { get; } = [];

    public DeviceType? SelectedDeviceType => m_SelectedSource?.DeviceType;

    public bool IsOutputEmpty => m_SelectedSource == null || !VisibleSources.Contains(m_SelectedSource);

    public string SelectedValueText
    {
        get
        {
            if (m_SelectedSource == null) { return "—"; }
            if (!VisibleSources.Contains(m_SelectedSource)) { return "—"; }
            return m_SelectedSource.DisplayText;
        }
    }
    #endregion

    #region Constructors
    public IntegrationFieldRowViewModel(string i_FieldName, string i_Unit)
    {
        FieldName = i_FieldName;
        Unit = i_Unit;
    }
    #endregion

    #region Functions
    // Rebuilds visible sources and keeps selection valid
    public void RefreshVisibleSources(Func<IntegrationSourceCandidateViewModel, bool> i_IsVisible)
    {
        VisibleSources.Clear();

        foreach (IntegrationSourceCandidateViewModel src in Sources)
        {
            if (!i_IsVisible(src)) { continue; }
            VisibleSources.Add(src);
        }

        EnforceValidSelection();
        SyncSelectionFlags();

        RefreshIntegratedOutput();
        OnPropertyChanged(nameof(IsOutputEmpty));
    }

    // Selects a source (row-owned selection)
    public void SelectSource(IntegrationSourceCandidateViewModel i_Source)
    {
        if (i_Source == null) { return; }
        if (ReferenceEquals(i_Source, m_SelectedSource)) { return; }

        SetSelectedSource(i_Source);

        SyncSelectionFlags();
        RefreshIntegratedOutput();
        OnPropertyChanged(nameof(IsOutputEmpty));
    }

    // Restores selection by DeviceType if possible
    public void RestoreSelection(DeviceType? i_PreviousDeviceType)
    {
        if (i_PreviousDeviceType == null) { EnforceValidSelection(); return; }

        IntegrationSourceCandidateViewModel? match = VisibleSources.FirstOrDefault(s => s.DeviceType == i_PreviousDeviceType);
        if (match != null) { SelectSource(match); return; }

        EnforceValidSelection();
    }

    // Ensures there is always a valid selection
    private void EnforceValidSelection()
    {
        if (m_SelectedSource != null && VisibleSources.Contains(m_SelectedSource)) { return; }

        IntegrationSourceCandidateViewModel? fallback = VisibleSources.FirstOrDefault();
        if (fallback != null) { SetSelectedSource(fallback); return; }

        ClearSelectedSource();
    }

    // Sets the selected source and manages subscriptions in one place
    private void SetSelectedSource(IntegrationSourceCandidateViewModel i_Source)
    {
        if (m_SelectedSource != null) { m_SelectedSource.PropertyChanged -= OnSelectedSourceChanged; }

        m_SelectedSource = i_Source;
        m_SelectedSource.PropertyChanged += OnSelectedSourceChanged;
    }

    // Clears selection and unsubscribes safely
    private void ClearSelectedSource()
    {
        if (m_SelectedSource == null) { return; }
        m_SelectedSource.PropertyChanged -= OnSelectedSourceChanged;
        m_SelectedSource = null;
    }

    // Keeps RadioButtons synced (selection state owned by row)
    private void SyncSelectionFlags()
    {
        foreach (IntegrationSourceCandidateViewModel src in Sources)
        {
            bool shouldBeSelected = ReferenceEquals(src, m_SelectedSource);
            if (src.IsSelected == shouldBeSelected) { continue; }
            src.SetSelected(shouldBeSelected);
        }
    }

    // Forces UI update
    private void RefreshIntegratedOutput() { OnPropertyChanged(nameof(SelectedValueText)); }
    #endregion

    #region Event Handlers
    // Keeps Integrated Output live when selected source value updates
    private void OnSelectedSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IntegrationSourceCandidateViewModel.DisplayText))
        {
            RefreshIntegratedOutput();
        }
    }
    #endregion
}
