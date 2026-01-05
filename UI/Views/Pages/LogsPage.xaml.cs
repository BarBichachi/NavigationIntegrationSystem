using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.Infrastructure.Logging;
using NavigationIntegrationSystem.UI.ViewModels;
using System;
using System.Collections.Specialized;

namespace NavigationIntegrationSystem.UI.Views.Pages;

// Shows the live log buffer with filtering and actions
public sealed partial class LogsPage : Page
{
    #region Private Fields
    private Action? m_RequestClearSelectionHandler;
    private bool m_IgnoreSelectionChanged;
    #endregion

    #region Properties
    public LogsViewModel ViewModel { get; }
    #endregion

    #region Ctors
    public LogsPage()
    {
        ViewModel = ((App)Application.Current).Services.GetRequiredService<LogsViewModel>();

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        m_RequestClearSelectionHandler = () =>
        {
            m_IgnoreSelectionChanged = true;

            LogsList.SelectedItems?.Clear();
            ViewModel.OnSelectionChanged(0);

            m_IgnoreSelectionChanged = false;
        };

        ViewModel.RequestClearSelection += m_RequestClearSelectionHandler;
    }
    #endregion

    #region Event Handlers
    // Scroll once when the page opens
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.OnPageOpened(LogsList);
    }

    // Detach event handlers to avoid memory leaks
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (m_RequestClearSelectionHandler != null)
        {
            ViewModel.RequestClearSelection -= m_RequestClearSelectionHandler;
            m_RequestClearSelectionHandler = null;
        }
    }


    // Forward selection count changes to the VM so it can update button texts
    private void OnLogsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (m_IgnoreSelectionChanged) { return; }
        ViewModel.OnSelectionChanged(LogsList.SelectedItems.Count);
    }
    #endregion
}