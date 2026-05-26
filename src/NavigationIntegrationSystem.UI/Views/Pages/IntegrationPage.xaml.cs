using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Layout;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Pages;

using System.Collections.Generic;

namespace NavigationIntegrationSystem.UI.Views.Pages;

// Displays the Integration grid and binds it to the IntegrationViewModel. Owns the cross-row horizontal-scroll sync: the header column-titles and every data row's source candidates live in their own ScrollViewers; this code-behind keeps their HorizontalOffset locked together so column names always align with their values regardless of where the user scrolled
public sealed partial class IntegrationPage : Page
{
    #region Properties
    public IntegrationViewModel ViewModel { get; }
    #endregion

    #region Private Fields
    // All currently-mounted source ScrollViewers (header + one per data row). Modified only on the UI thread via Loaded/Unloaded. List rather than HashSet because the count is small (typically <= 1 + visible-rows) and order doesn't matter
    private readonly List<ScrollViewer> m_SourceScrollers = new();
    // Re-entrancy guard. ChangeView fires ViewChanged on the target ScrollViewer; without this guard a single user scroll would cascade into N^2 events
    private bool m_IsSyncingScroll;
    #endregion

    #region Constructors
    public IntegrationPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<IntegrationViewModel>();
        ViewModel.Initialize(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

        this.Unloaded += (s, e) => ViewModel.Deinitialize();
    }
    #endregion

    #region Event Handlers
    // Routes candidate selection click to the owning row
    private void OnSourceRadioButtonClicked(object i_Sender, RoutedEventArgs i_E)
    {
        if (i_Sender is not RadioButton radioButton) { return; }
        if (radioButton.DataContext is not IntegrationSourceCandidateViewModel src) { return; }

        DependencyObject current = radioButton;
        while (current != null && current is not ListViewItem) { current = VisualTreeHelper.GetParent(current); }
        if (current is not ListViewItem listViewItem) { return; }
        if (listViewItem.Content is not IntegrationFieldRowViewModel row) { return; }

        row.SelectSource(src);
    }

    // Registers a row/header source-ScrollViewer when it enters the visual tree. ListView virtualization causes data-row ScrollViewers to load/unload as the user scrolls vertically, so subscription happens here rather than in the constructor
    private void OnSourcesScrollerLoaded(object i_Sender, RoutedEventArgs i_E)
    {
        if (i_Sender is not ScrollViewer scroller) { return; }
        if (m_SourceScrollers.Contains(scroller)) { return; }

        m_SourceScrollers.Add(scroller);
        scroller.ViewChanged += OnSourcesScrollerViewChanged;
        // Snap the new ScrollViewer to the current shared offset so freshly-loaded rows match what the user already scrolled to. Picks any existing scroller (header preferred) to read from
        ScrollViewer? reference = m_SourceScrollers.Count > 1 ? m_SourceScrollers[0] : null;
        if (reference != null && !ReferenceEquals(reference, scroller))
        {
            m_IsSyncingScroll = true;
            try { scroller.ChangeView(reference.HorizontalOffset, null, null, true); }
            finally { m_IsSyncingScroll = false; }
        }
    }

    // Unsubscribes when the ScrollViewer leaves the tree (row recycled by virtualization, or page closed)
    private void OnSourcesScrollerUnloaded(object i_Sender, RoutedEventArgs i_E)
    {
        if (i_Sender is not ScrollViewer scroller) { return; }
        scroller.ViewChanged -= OnSourcesScrollerViewChanged;
        m_SourceScrollers.Remove(scroller);
    }

    // Propagates a scroll on any source ScrollViewer (header or row) to every other one. Skips intermediate events for performance; only final positions are mirrored. ChangeView's `disableAnimation = true` makes the mirror snap instantly without easing
    private void OnSourcesScrollerViewChanged(object? i_Sender, ScrollViewerViewChangedEventArgs i_Args)
    {
        if (m_IsSyncingScroll) { return; }
        if (i_Args.IsIntermediate) { return; }
        if (i_Sender is not ScrollViewer source) { return; }

        double offset = source.HorizontalOffset;
        m_IsSyncingScroll = true;
        try
        {
            for (int i = 0; i < m_SourceScrollers.Count; i++)
            {
                ScrollViewer other = m_SourceScrollers[i];
                if (ReferenceEquals(other, source)) { continue; }
                if (other.HorizontalOffset == offset) { continue; }
                other.ChangeView(offset, null, null, true);
            }
        }
        finally
        {
            m_IsSyncingScroll = false;
        }
    }
    #endregion
}
