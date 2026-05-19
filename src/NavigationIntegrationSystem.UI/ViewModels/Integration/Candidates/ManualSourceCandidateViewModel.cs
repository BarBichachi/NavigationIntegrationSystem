using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;
using System.Globalization;
using System.Threading;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;

// Candidate backed by user input (numeric text)
public sealed class ManualSourceCandidateViewModel : IntegrationSourceCandidateViewModel
{
    #region Private Fields
    private string m_Text = string.Empty;
    private bool m_IsValid;
    private double m_Value;
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

    public double Value => Volatile.Read(ref m_Value);

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
    public ManualSourceCandidateViewModel(string i_DisplayName) : base(DeviceType.Manual, i_DisplayName)
    {
        SourceDevice = null;
        Validate();
    }
    #endregion

    #region Functions
    // Validates Text and forces DisplayText update; writes m_Value via Volatile.Write for background snapshot reads
    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            Volatile.Write(ref m_Value, 0);
            IsValid = true;
            return;
        }

        bool ok = double.TryParse(Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed);
        Volatile.Write(ref m_Value, parsed);
        IsValid = ok;
        OnPropertyChanged(nameof(DisplayText));
    }

    // Thread-safe read for the background snapshot loop
    public override double GetSnapshotValue() => Volatile.Read(ref m_Value);
    #endregion
}
