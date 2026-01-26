using CommunityToolkit.Mvvm.Input;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Base;
using System;
using System.Windows.Input;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Layout;

// Represents a device option in the Integration header (visibility toggle + apply-to-all)
public sealed partial class IntegrationDeviceOptionViewModel : ViewModelBase
{
    #region Private Fields
    private bool m_IsVisible;
    #endregion

    #region Properties
    public DeviceType DeviceType { get; }
    public string DisplayName { get; }

    public bool IsVisible
    {
        get => m_IsVisible;
        set
        {
            if (!SetProperty(ref m_IsVisible, value)) { return; }
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    #endregion

    #region Events
    public event EventHandler? VisibilityChanged;
    public event EventHandler? ApplyToAllRequested;
    #endregion

    #region Commands
    public ICommand ApplyToAllFieldsCommand { get; }
    #endregion

    #region Constructors
    public IntegrationDeviceOptionViewModel(DeviceType i_DeviceType, string i_DisplayName, bool i_IsVisible)
    {
        DeviceType = i_DeviceType;
        DisplayName = i_DisplayName;
        m_IsVisible = i_IsVisible;

        ApplyToAllFieldsCommand = new RelayCommand(() => ApplyToAllRequested?.Invoke(this, EventArgs.Empty));
    }
    #endregion
}