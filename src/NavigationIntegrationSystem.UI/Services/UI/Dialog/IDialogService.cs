using Microsoft.UI.Xaml;
using NavigationIntegrationSystem.UI.Enums;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.Services.UI.Dialog;

// Provides application dialogs
public interface IDialogService
{
    Task<DialogCloseDecision> ShowUnsavedChangesDialogAsync(XamlRoot i_XamlRoot);

    Task ShowValidationFailedDialogAsync(XamlRoot i_XamlRoot, string i_Summary);
}