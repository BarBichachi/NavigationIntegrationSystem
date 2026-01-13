using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NavigationIntegrationSystem.UI.ViewModels;
using System;

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
            ViewModel.SelectedItems = LogsList.SelectedItems;
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

        ViewModel.SelectedItems = LogsList.SelectedItems;
        ViewModel.OnSelectionChanged(LogsList.SelectedItems.Count);
    }
    #endregion

    #region Keyboard Shortcuts
    // Handles Ctrl+C copy shortcut
    private async void OnCopyAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.CopyCommand.ExecuteAsync(LogsList.SelectedItems);
    }

    // Handles Ctrl+A select/deselect all shortcut
    private void OnSelectAllAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.ToggleSelectAllCommand.Execute(LogsList);
    }

    // Handles Delete clear shortcut
    private void OnDeleteAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ViewModel.ClearCommand.Execute(LogsList.SelectedItems);
    }

    // Handles Esc to clear selection shortcut
    private void OnEscapeAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        m_IgnoreSelectionChanged = true;

        LogsList.SelectedItems?.Clear();
        ViewModel.OnSelectionChanged(0);

        m_IgnoreSelectionChanged = false;
    }
    #endregion
}