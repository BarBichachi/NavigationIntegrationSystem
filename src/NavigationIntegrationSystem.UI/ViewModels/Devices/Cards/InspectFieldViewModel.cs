using CommunityToolkit.Mvvm.ComponentModel;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;

// Represents a single field row in the Inspect pane with a live value string
public sealed partial class InspectFieldViewModel : ObservableObject
{
    #region Private Fields
    private string m_ValueText;
    #endregion

    #region Properties
    public string Key { get; }
    public string Name { get; }
    public string Unit { get; }

    public string ValueText
    {
        get => m_ValueText;
        set => SetProperty(ref m_ValueText, value);
    }
    #endregion

    #region Ctors
    public InspectFieldViewModel(string i_Key, string i_Name, string i_Unit)
    {
        Key = i_Key;
        Name = i_Name;
        Unit = i_Unit;
        m_ValueText = string.Empty;
    }
    #endregion
}