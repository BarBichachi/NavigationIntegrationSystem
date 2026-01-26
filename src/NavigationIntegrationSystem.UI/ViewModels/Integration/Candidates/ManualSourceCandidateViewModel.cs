using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;
using System.Globalization;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// Candidate backed by user input (numeric text)
public sealed partial class ManualSourceCandidateViewModel : IntegrationSourceCandidateViewModel
{
    #region Private Fields
    private string m_Text = string.Empty;
    private bool m_IsValid;
    #endregion

    #region Properties
    public string Text
    {
        get => m_Text;
        set
        {
            if (!SetProperty(ref m_Text, value)) { return; }
            Validate();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public bool IsValid { get => m_IsValid; private set => SetProperty(ref m_IsValid, value); }

    public override string DisplayText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Text)) { return "—"; }
            if (!IsValid) { return "Invalid"; }
            return Text.Trim();
        }
    }
    #endregion

    #region Ctors
    public ManualSourceCandidateViewModel(string i_DisplayName) : base(DeviceType.Manual, i_DisplayName) { Validate(); }
    #endregion

    #region Functions
    // Validates Text as an invariant-culture double (allows empty)
    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Text)) { IsValid = true; return; }
        IsValid = double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
    #endregion
}
