using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.UI.Services.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace NavigationIntegrationSystem.UI.ViewModels;

// Exposes the live log buffer for UI binding and future log actions
public sealed partial class LogsViewModel : ObservableObject
{
    #region Private Fields
    private readonly UiLogBuffer m_LogBuffer;
    private readonly ILogPaths m_LogPaths;
    private DispatcherQueue? m_DispatcherQueue;
    private AdvancedCollectionView m_FilteredEntries;
    private IList<object>? m_SelectedItems;
    private string m_SearchText = string.Empty;
    private string m_SelectedLevel = "All";
    private string m_CopyButtonText = "Copy all";
    private string m_ClearButtonText = "Clear all";
    private string m_SelectAllButtonText = "Select all";
    private int m_LastSelectedCount;
    #endregion

    #region Properties
    public ObservableCollection<UiLogEntry> Entries => m_LogBuffer.Entries;
    public AdvancedCollectionView FilteredEntries => m_FilteredEntries;
    public string LogFolderPath => m_LogPaths.LogFolderPath;

    public string SearchText
    {
        get => m_SearchText;
        set
        {
            if (!SetProperty(ref m_SearchText, value)) { return; }
            FilteredEntries.Refresh();
            RequestSelectionClear();
        }
    }

    public string SelectedLevel
    {
        get => m_SelectedLevel;
        set
        {
            if (!SetProperty(ref m_SelectedLevel, value)) { return; }
            FilteredEntries.Refresh();
            RequestSelectionClear();
        }
    }

    public string CopyButtonText { get => m_CopyButtonText; private set => SetProperty(ref m_CopyButtonText, value); }
    public string ClearButtonText { get => m_ClearButtonText; private set => SetProperty(ref m_ClearButtonText, value); }
    public string SelectAllButtonText { get => m_SelectAllButtonText; private set => SetProperty(ref m_SelectAllButtonText, value); }
    public IList<object>? SelectedItems { get => m_SelectedItems; set => SetProperty(ref m_SelectedItems, value); }
    #endregion

    #region Commands
    public IAsyncRelayCommand<IList<object>?> CopyCommand { get; }
    public IRelayCommand<IList<object>?> ClearCommand { get; }
    public IRelayCommand<ListView?> ToggleSelectAllCommand { get; }
    public IAsyncRelayCommand OpenLogFolderCommand { get; }
    #endregion

    #region Events
    public event Action? RequestClearSelection;
    #endregion

    #region Ctors
    public LogsViewModel(UiLogBuffer i_LogBuffer, ILogPaths i_LogPaths)
    {
        m_LogBuffer = i_LogBuffer;
        m_LogPaths = i_LogPaths;

        m_FilteredEntries = new AdvancedCollectionView(m_LogBuffer.Entries, true);
        m_FilteredEntries.Filter = FilterLogEntry;

        CopyCommand = new AsyncRelayCommand<IList<object>?>(ExecuteCopyAsync);
        ClearCommand = new RelayCommand<IList<object>?>(ExecuteClear);
        ToggleSelectAllCommand = new RelayCommand<ListView?>(ExecuteToggleSelectAll);
        OpenLogFolderCommand = new AsyncRelayCommand(ExecuteOpenLogFolderAsync);

        UpdateSelectionButtons(0, m_FilteredEntries.Count);
    }
    #endregion

    #region Functions
    // Filters entries by current level and search text
    private bool FilterLogEntry(object i_Item)
    {
        if (i_Item is not UiLogEntry entry) { return false; }

        if (!string.Equals(SelectedLevel, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.Level, SelectedLevel, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            if ((entry.Message?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) ?? -1) < 0) { return false; }
        }

        return true;
    }

    // Copies selected rows (if any); otherwise copies all filtered rows, and shows "Copied"
    private async Task ExecuteCopyAsync(IList<object>? i_SelectedItems)
    {
        var sb = new StringBuilder(Entries.Count * 64);

        if (i_SelectedItems != null && i_SelectedItems.Count > 0)
        {
            var snapshot = new object[i_SelectedItems.Count];
            i_SelectedItems.CopyTo(snapshot, 0);

            foreach (object item in snapshot)
            {
                if (item is UiLogEntry e) { AppendEntryLine(sb, e); }
            }
        }
        else
        {
            foreach (var item in m_FilteredEntries)
            {
                if (item is UiLogEntry e) { AppendEntryLine(sb, e); }
            }
        }

        var data = new DataPackage();
        data.SetText(sb.ToString());
        Clipboard.SetContent(data);

        CopyButtonText = "Copied";
        await Task.Delay(900);

        int selectedCount = m_LastSelectedCount;
        DispatcherQueue? dq = m_DispatcherQueue;

        if (dq == null || dq.HasThreadAccess) { UpdateSelectionButtons(selectedCount, m_FilteredEntries.Count); }
        else { dq.TryEnqueue(() => { UpdateSelectionButtons(selectedCount, m_FilteredEntries.Count); }); }
    }

    // Clears selected rows (if any); otherwise clears all
    private void ExecuteClear(IList<object>? i_SelectedItems)
    {
        if (i_SelectedItems != null && i_SelectedItems.Count > 0)
        {
            var snapshot = new object[i_SelectedItems.Count];
            i_SelectedItems.CopyTo(snapshot, 0);

            foreach (object item in snapshot)
            {
                if (item is UiLogEntry e) { m_LogBuffer.Remove(e); }
            }

            RequestSelectionClear();
            return;
        }

        m_LogBuffer.Clear();
        RequestSelectionClear();
    }

    // Select all / deselect all visible rows
    private void ExecuteToggleSelectAll(ListView? i_ListView)
    {
        if (i_ListView == null) { return; }

        int total = m_FilteredEntries.Count;
        int selected = i_ListView.SelectedItems.Count;

        if (total > 0 && selected == total)
        {
            i_ListView.SelectedItems.Clear();
            UpdateSelectionButtons(0, total);
            return;
        }

        i_ListView.SelectedItems.Clear();

        // Add only LogEntry items (defensive)
        foreach (var item in m_FilteredEntries)
        {
            if (item is UiLogEntry)
            {
                i_ListView.SelectedItems.Add(item);
            }
        }

        UpdateSelectionButtons(i_ListView.SelectedItems.Count, total);
    }

    // Scroll once (page open)
    private void ScrollToBottom(ListView i_ListView)
    {
        int count = m_FilteredEntries.Count;
        if (count <= 0) { return; }

        object last = m_FilteredEntries[count - 1];
        m_DispatcherQueue?.TryEnqueue(() => { i_ListView.ScrollIntoView(last); });
    }

    // Updates button labels based on selection
    private void UpdateSelectionButtons(int i_SelectedCount, int i_TotalVisible)
    {
        ClearButtonText = i_SelectedCount > 0 ? "Clear selected" : "Clear all";
        SelectAllButtonText = (i_TotalVisible > 0 && i_SelectedCount == i_TotalVisible) ? "Deselect all" : "Select all";
        CopyButtonText = i_SelectedCount > 0 ? "Copy selected" : "Copy all";
    }

    // Appends a formatted log line into the builder
    private static void AppendEntryLine(StringBuilder i_Sb, UiLogEntry i_Entry)
    {
        i_Sb.Append(i_Entry.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        i_Sb.Append(" [");
        i_Sb.Append(i_Entry.Level);
        i_Sb.Append("] ");
        i_Sb.AppendLine(i_Entry.Message);
    }

    // Opens the logs folder in Explorer
    private async Task ExecuteOpenLogFolderAsync()
    {
        try
        {
            StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(LogFolderPath);
            await Launcher.LaunchFolderAsync(folder);
        }
        catch { }
    }

    // Called by the page after it loads to scroll once to bottom
    public void OnPageOpened(ListView? i_ListView)
    {
        if (i_ListView == null) { return; }
        m_DispatcherQueue = i_ListView.DispatcherQueue;
        ScrollToBottom(i_ListView);
    }

    // Called by the page when selection changes (so VM updates button text)
    public void OnSelectionChanged(int i_SelectedCount)
    {
        m_LastSelectedCount = i_SelectedCount;
        UpdateSelectionButtons(i_SelectedCount, m_FilteredEntries.Count);
    }

    // Requests the view to clear ListView selection and updates button texts accordingly
    private void RequestSelectionClear()
    {
        RequestClearSelection?.Invoke();
        UpdateSelectionButtons(0, m_FilteredEntries.Count);
        m_LastSelectedCount = 0;
    }
    #endregion
}
