using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using NavigationIntegrationSystem.Infrastructure.Logging;

namespace NavigationIntegrationSystem.UI.ViewModels;

// Exposes the live log buffer for UI binding and future log actions
public sealed partial class LogsViewModel : ObservableObject
{
    #region Private Fields
    private readonly LogService m_LogService;
    #endregion

    #region Properties
    public ObservableCollection<LogEntry> Entries => m_LogService.Entries;
    #endregion

    #region Ctors
    public LogsViewModel(LogService i_LogService) { m_LogService = i_LogService; }
    #endregion
}
