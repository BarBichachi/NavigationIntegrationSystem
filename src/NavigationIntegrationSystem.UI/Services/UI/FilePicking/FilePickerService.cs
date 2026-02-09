using NavigationIntegrationSystem.UI.Services.UI.Windowing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NavigationIntegrationSystem.UI.Services.UI.FilePicking;

// Concrete implementation of IFilePickerService using UWP pickers
public sealed class FilePickerService : IFilePickerService
{
    #region Private Fields
    private readonly IWindowProvider m_WindowProvider;
    #endregion

    #region Constructors
    public FilePickerService(IWindowProvider i_WindowProvider)
    {
        m_WindowProvider = i_WindowProvider;
    }
    #endregion

    #region Functions

    // Picks a single file with the specified extensions and returns its path (or null if cancelled)
    public async Task<string?> PickSingleFileAsync(IEnumerable<string> i_Extensions)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow(picker);

        picker.ViewMode = PickerViewMode.List;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        foreach (string ext in i_Extensions)
        {
            picker.FileTypeFilter.Add(ext);
        }

        StorageFile file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    // Picks a save file location with the specified name and type choices, returns the chosen path (or null if cancelled)
    public async Task<string?> PickSaveFileAsync(string i_FileName, IDictionary<string, IList<string>> i_FileTypeChoices)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow(picker);

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = i_FileName;

        foreach (var choice in i_FileTypeChoices)
        {
            picker.FileTypeChoices.Add(choice.Key, choice.Value);
        }

        StorageFile file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    // Associates the picker with the main window handle using the provider
    private void InitializeWithWindow(object i_Target)
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(m_WindowProvider.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(i_Target, hwnd);
    }
    #endregion
}