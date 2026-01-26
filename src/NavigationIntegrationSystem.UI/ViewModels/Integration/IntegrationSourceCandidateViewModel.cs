using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Base;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Base VM for a per-row candidate source (device-provided or manual)
public abstract partial class IntegrationSourceCandidateViewModel : ViewModelBase
{
    #region Private Fields
    private bool m_IsSelected;
    #endregion

    #region Properties
    public DeviceType DeviceType { get; }
    public string DisplayName { get; }
    public abstract string DisplayText { get; }
    public bool IsSelected { get => m_IsSelected; private set => SetProperty(ref m_IsSelected, value); }
    #endregion

    #region Ctors
    protected IntegrationSourceCandidateViewModel(DeviceType i_DeviceType, string i_DisplayName)
    {
        DeviceType = i_DeviceType;
        DisplayName = i_DisplayName;
    }
    #endregion

    #region Functions
    // Sets selection state (owned by the row)
    internal void SetSelected(bool i_IsSelected) { IsSelected = i_IsSelected; }

    // Optional tick hook (manual does nothing)
    public virtual void Tick(double i_StepScale) { }
    #endregion
}
