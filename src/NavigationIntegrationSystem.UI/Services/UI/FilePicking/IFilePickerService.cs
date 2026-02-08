using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.Services.UI.FilePicking;

// Abstraction for opening/saving files to keep ViewModels decoupled from UI handles
public interface IFilePickerService
{
    // Opens a file picker for a single file
    Task<string?> PickSingleFileAsync(IEnumerable<string> i_Extensions);

    // Opens a save file picker
    Task<string?> PickSaveFileAsync(string i_FileName, IDictionary<string, IList<string>> i_FileTypeChoices);
}