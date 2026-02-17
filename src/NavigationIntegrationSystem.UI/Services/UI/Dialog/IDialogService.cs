using Microsoft.UI.Xaml;
using NavigationIntegrationSystem.UI.Enums;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.Services.UI.Dialog;

// Provides application dialogs
public interface IDialogService
{
    // Shows a dialog asking the user whether to apply, discard, or cancel changes
    Task<DialogCloseDecision> ShowUnsavedChangesDialogAsync(XamlRoot i_XamlRoot);

    // Shows a validation failed dialog with a summary of all errors
    Task ShowValidationFailedDialogAsync(XamlRoot i_XamlRoot, string i_Summary);

    // Shows a generic error dialog
    Task ShowErrorAsync(string i_Title, string i_Message);

    // Shows a generic info dialog
    Task ShowInfoAsync(string i_Title, string i_Message);
}