// FILE: src\NavigationIntegrationSystem.UI\ViewModels\Settings\SettingsViewModel.cs
using System;
using System.Diagnostics;
using System.IO;

using CommunityToolkit.Mvvm.Input;

using NavigationIntegrationSystem.UI.ViewModels.Base;

namespace NavigationIntegrationSystem.UI.ViewModels.Settings;

public sealed partial class SettingsViewModel : ViewModelBase
{
    #region Commands
    public IRelayCommand OpenRecordingsFolderCommand { get; }
    #endregion

    #region Constructors
    public SettingsViewModel()
    {
        OpenRecordingsFolderCommand = new RelayCommand(OnOpenRecordingsFolder);
    }
    #endregion

    #region Command Handlers
    // Opens the folder where recordings are stored, creating it if it doesn't exist
    private void OnOpenRecordingsFolder()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings");

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception)
        {
            // Fallback for environment issues
        }
    }
    #endregion
}