using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.Services.UI.Dialog;

// Provides application dialogs
public interface IDialogService
{
    Task<DialogCloseDecision> ShowUnsavedChangesDialogAsync(XamlRoot i_XamlRoot);
}