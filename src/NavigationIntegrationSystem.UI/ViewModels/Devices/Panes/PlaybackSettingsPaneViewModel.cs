using CommunityToolkit.Mvvm.Input;
using NavigationIntegrationSystem.Core.Playback;
using NavigationIntegrationSystem.Devices.Validation;
using NavigationIntegrationSystem.UI.Services.UI.Dialog;
using NavigationIntegrationSystem.UI.Services.UI.FilePicking;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

// Settings pane for Playback (CSV-source). Adds the file picker + export-template commands + the allowed frequency list on top of the common base
public sealed partial class PlaybackSettingsPaneViewModel : DeviceSettingsPaneViewModelBase
{
    #region Properties
    public IReadOnlyList<int> Frequencies => PlaybackFrequencies.All;
    #endregion

    #region Private Fields
    private readonly IFilePickerService m_FilePickerService;
    private readonly IPlaybackService m_PlaybackService;
    #endregion

    #region Commands
    public IAsyncRelayCommand BrowseFileCommand { get; }
    public IAsyncRelayCommand ExportTemplateCommand { get; }
    #endregion

    #region Constructors
    public PlaybackSettingsPaneViewModel(DevicesViewModel i_Parent, DeviceCardViewModel i_Device, IDialogService i_DialogService,
        IFilePickerService i_FilePickerService, IPlaybackService i_PlaybackService)
        : base(i_Parent, i_Device, i_DialogService)
    {
        m_FilePickerService = i_FilePickerService;
        m_PlaybackService = i_PlaybackService;

        BrowseFileCommand = new AsyncRelayCommand(OnBrowseFileAsync);
        ExportTemplateCommand = new AsyncRelayCommand(OnExportTemplateAsync);
    }
    #endregion

    #region Event Handlers
    // Opens a file picker for CSV files and updates the Draft's PlaybackFilePath if a file is selected
    private async Task OnBrowseFileAsync()
    {
        string? path = await m_FilePickerService.PickSingleFileAsync(new[] { ".csv" });
        if (!string.IsNullOrEmpty(path)) { Draft.PlaybackFilePath = path; }
    }

    // Opens a file picker to select where to save a CSV template, then calls the PlaybackService to create it
    private async Task OnExportTemplateAsync()
    {
        string? path = await m_FilePickerService.PickSaveFileAsync("Playback_Template.csv", new Dictionary<string, IList<string>> { { "CSV", new[] { ".csv" } } });
        if (!string.IsNullOrEmpty(path)) { await m_PlaybackService.CreateTemplateAsync(path); }
    }
    #endregion
}
