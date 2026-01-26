using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.Enums;
using System;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.UI.Services.UI.Dialog;

// Handles user-facing dialogs
public sealed class DialogService : IDialogService
{
    #region Functions
    // Shows a confirmation dialog for unsaved changes
    public async Task<DialogCloseDecision> ShowUnsavedChangesDialogAsync(XamlRoot i_XamlRoot)
    {
        ContentDialog dialog = new ContentDialog
        {
            Title = "Unsaved changes",
            Content = "Changes won't take effect unless you apply them. What would you like to do?",
            PrimaryButtonText = "Apply",
            SecondaryButtonText = "Discard",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = i_XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync().AsTask();

        if (result == ContentDialogResult.Primary)
        { return DialogCloseDecision.Apply; }
        if (result == ContentDialogResult.Secondary)
        { return DialogCloseDecision.Discard; }

        return DialogCloseDecision.Cancel;
    }

    // Shows a validation-failed dialog with summary
    public async Task ShowValidationFailedDialogAsync(XamlRoot i_XamlRoot, string i_Summary)
    {
        ContentDialog dialog = new ContentDialog
        {
            Title = "Invalid settings",
            Content = i_Summary,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = i_XamlRoot
        };

        await dialog.ShowAsync().AsTask();
    }

    #endregion
}