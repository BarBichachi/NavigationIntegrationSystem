namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// Decoded view of the VN310's 16-bit InsStatus word. Bit layout per VN310 ICD: bits 0-1 = Mode, bit 2 = GpsFix, bits 3-6 = MeasurementError, bit 8 = GpsHeadingIns, bit 9 = GpsCompassActive
public sealed class Vn310InsStatus
{
    #region Properties
    public ushort RawData { get; set; }

    public Vn310InsMode Mode => (Vn310InsMode)(RawData & 0x3);

    public bool IsGpsFix => ((RawData >> 2) & 0x1) != 0;

    public byte MeasurementError => (byte)((RawData >> 3) & 0xF);

    public Vn310InsMeasurementErrors InsErrors => new Vn310InsMeasurementErrors { RawData = MeasurementError };

    public bool IsGpsHeadingIns => ((RawData >> 8) & 0x1) != 0;

    public bool IsGpsCompassActive => ((RawData >> 9) & 0x1) != 0;
    #endregion

    #region Functions
    // Returns a deep copy of this status snapshot
    public Vn310InsStatus Clone()
    {
        return new Vn310InsStatus { RawData = this.RawData };
    }
    #endregion
}

// Decoded view of the 4-bit MeasurementError sub-field. Bit layout: bit 1 = IMU, bit 2 = Magnetometer, bit 3 = GNSS
public sealed class Vn310InsMeasurementErrors
{
    #region Properties
    public byte RawData { get; set; }

    public bool IsImuError => ((RawData >> 1) & 0x1) != 0;

    public bool IsMagnetometerError => ((RawData >> 2) & 0x1) != 0;

    public bool IsGnssError => ((RawData >> 3) & 0x1) != 0;
    #endregion
}
