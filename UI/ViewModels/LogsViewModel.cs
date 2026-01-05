using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.Infrastructure.Logging;
using System;
using System.Collections;
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
    private readonly LogService m_LogService;
    private DispatcherQueue? m_DispatcherQueue;
    private AdvancedCollectionView m_FilteredEntries;
    private string m_SearchText = string.Empty;
    private string m_SelectedLevel = "All";
    private string m_CopyButtonText = "Copy all";
    private string m_ClearButtonText = "Clear all";
    private string m_SelectAllButtonText = "Select all";
    private int m_LastSelectedCount;
    #endregion

    #region Properties
    public ObservableCollection<LogEntry> Entries => m_LogService.Entries;
    public AdvancedCollectionView FilteredEntries => m_FilteredEntries;
    public string LogFolderPath => m_LogService.LogFolderPath;

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
    #endregion

    #region Commands
    public IAsyncRelayCommand<ListView?> CopyCommand { get; }
    public IRelayCommand<ListView?> ClearCommand { get; }
    public IRelayCommand<ListView?> ToggleSelectAllCommand { get; }
    public IAsyncRelayCommand OpenLogFolderCommand { get; }
    #endregion

    #region Events
    public event Action? RequestClearSelection;
    #endregion

    #region Ctors
    public LogsViewModel(LogService i_LogService)
    {
        m_LogService = i_LogService;
        m_FilteredEntries = new AdvancedCollectionView(m_LogService.Entries, true);
        m_FilteredEntries.Filter = FilterLogEntry;

        CopyCommand = new AsyncRelayCommand<ListView?>(ExecuteCopyAsync);
        ClearCommand = new RelayCommand<ListView?>(ExecuteClear);
        ToggleSelectAllCommand = new RelayCommand<ListView?>(ExecuteToggleSelectAll);
        OpenLogFolderCommand = new AsyncRelayCommand(ExecuteOpenLogFolderAsync);

        UpdateSelectionButtons(0, m_FilteredEntries.Count);
    }
    #endregion

    #region Functions
    // Filters entries by current level and search text
    private bool FilterLogEntry(object i_Item)
    {
        if (i_Item is not LogEntry entry) { return false; }

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
    private async Task ExecuteCopyAsync(ListView? i_ListView)
    {
        var sb = new StringBuilder(Entries.Count * 64);

        object[] selectedSnapshot = Array.Empty<object>();

        if (i_ListView?.SelectedItems != null && i_ListView.SelectedItems.Count > 0)
        {
            selectedSnapshot = new object[i_ListView.SelectedItems.Count];
            i_ListView.SelectedItems.CopyTo(selectedSnapshot, 0);
        }

        if (selectedSnapshot.Length > 0)
        {
            foreach (var item in selectedSnapshot)
            {
                if (item is LogEntry e) { AppendEntryLine(sb, e); }
            }
        }
        else
        {
            foreach (var item in m_FilteredEntries)
            {
                if (item is LogEntry e) { AppendEntryLine(sb, e); }
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
    private void ExecuteClear(ListView? i_ListView)
    {
        if (i_ListView?.SelectedItems != null && i_ListView.SelectedItems.Count > 0)
        {
            object[] snapshot = new object[i_ListView.SelectedItems.Count];
            i_ListView.SelectedItems.CopyTo(snapshot, 0);

            foreach (var item in snapshot)
            {
                if (item is LogEntry e) { Entries.Remove(e); }
            }

            RequestSelectionClear();
            return;
        }

        m_LogService.ClearUiEntries();
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
            if (item is LogEntry)
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
    private static void AppendEntryLine(StringBuilder i_Sb, LogEntry i_Entry)
    {
        i_Sb.Append(i_Entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
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
        m_LastSelectedCount = 0;
        RequestClearSelection?.Invoke();
        UpdateSelectionButtons(0, m_FilteredEntries.Count);
    }
    #endregion
}
