using System.Threading.Tasks;

using Microsoft.UI.Xaml;

namespace NavigationIntegrationSystem.Services.UI.Dialog;

// Provides application dialogs
public interface IDialogService
{
    Task<DialogCloseDecision> ShowUnsavedChangesDialogAsync(XamlRoot i_XamlRoot);
}