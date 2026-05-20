namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// Decoded view of the VN310's 8-bit TimeStatus byte. Bit layout per VN310 ICD: bit 0 = TimeOK, bit 1 = DateOK, bit 2 = UtcTimeValid
public sealed class Vn310TimeStatus
{
    #region Properties
    public byte RawData { get; set; }

    public bool IsTimeOK => (RawData & 0x1) != 0;

    public bool IsDateOK => ((RawData >> 1) & 0x1) != 0;

    public bool IsUtcTimeValid => ((RawData >> 2) & 0x1) != 0;

    // Composite validity: all three sub-flags must be true for the embedded UTC timestamp to be trustworthy
    public bool IsValid => IsTimeOK && IsDateOK && IsUtcTimeValid;
    #endregion

    #region Functions
    // Returns a deep copy of this status snapshot
    public Vn310TimeStatus Clone()
    {
        return new Vn310TimeStatus { RawData = this.RawData };
    }
    #endregion
}
