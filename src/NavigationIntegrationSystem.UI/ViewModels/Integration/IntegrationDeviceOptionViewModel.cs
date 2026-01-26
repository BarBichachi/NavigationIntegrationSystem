using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NavigationIntegrationSystem.Core.Enums;
using System;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Represents a connected device option in the Integration header (visible toggle + apply-to-all)
public sealed partial class IntegrationDeviceOptionViewModel : ObservableObject
{
    #region Private Fields
    private bool m_IsVisible;
    private readonly Action<DeviceType> m_ApplyToAll;
    #endregion

    #region Properties
    public DeviceType DeviceType { get; }
    public string DisplayName { get; }
    public bool IsVisible { get => m_IsVisible; set => SetProperty(ref m_IsVisible, value); }
    public bool IsManual => DeviceType == DeviceType.Manual;
    #endregion

    #region Commands
    public IRelayCommand ApplyToAllFieldsCommand { get; }
    #endregion

    #region Ctors
    public IntegrationDeviceOptionViewModel(DeviceType i_DeviceType, string i_DisplayName, bool i_IsVisible, Action<DeviceType> i_ApplyToAll)
    {
        DeviceType = i_DeviceType;
        DisplayName = i_DisplayName;
        m_IsVisible = i_IsVisible;
        m_ApplyToAll = i_ApplyToAll;
        ApplyToAllFieldsCommand = new RelayCommand(OnApplyToAllFields);
    }
    #endregion

    #region Functions
    // Applies this device as the selected source for all rows (where available)
    private void OnApplyToAllFields() { m_ApplyToAll(DeviceType); }
    #endregion
}