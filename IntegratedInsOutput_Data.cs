using System;
using System.IO;

using Infrastructure.DataStructures;
using Infrastructure.Navigation.EulerCalculations;

namespace Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;

public sealed class IntegratedInsOutput_Data
{
    #region Private Fields
    private DateTime m_OutputTime;
    private WGS84Data m_Position;
    private EulerData m_EulerData;
    private NEDData m_NedVelocity;
    private double m_Course;
    private uint m_StatusValue;
    private readonly object m_LockObject;
    #endregion

    #region Properties

    // Output time (selected source)
    public ushort OutputTimeDeviceCode { get; set; }
    public ushort OutputTimeDeviceId { get; set; }
    public DateTime OutputTime { get { lock (m_LockObject) { return m_OutputTime; } } set { lock (m_LockObject) { m_OutputTime = value; } } }

    // Position sources (per field)
    public ushort LatitudeDeviceCode { get; set; }
    public ushort LatitudeDeviceId { get; set; }
    public ushort LongitudeDeviceCode { get; set; }
    public ushort LongitudeDeviceId { get; set; }
    public ushort AltitudeDeviceCode { get; set; }
    public ushort AltitudeDeviceId { get; set; }

    // Euler angles sources (per field)
    public ushort RollDeviceCode { get; set; }
    public ushort RollDeviceId { get; set; }
    public ushort PitchDeviceCode { get; set; }
    public ushort PitchDeviceId { get; set; }
    public ushort AzimuthDeviceCode { get; set; }
    public ushort AzimuthDeviceId { get; set; }

    // Euler rates sources (per field)
    public ushort RollRateDeviceCode { get; set; }
    public ushort RollRateDeviceId { get; set; }
    public ushort PitchRateDeviceCode { get; set; }
    public ushort PitchRateDeviceId { get; set; }
    public ushort AzimuthRateDeviceCode { get; set; }
    public ushort AzimuthRateDeviceId { get; set; }

    // Velocity sources (per field)
    public ushort VelocityTotalDeviceCode { get; set; }
    public ushort VelocityTotalDeviceId { get; set; }
    public ushort VelocityNorthDeviceCode { get; set; }
    public ushort VelocityNorthDeviceId { get; set; }
    public ushort VelocityEastDeviceCode { get; set; }
    public ushort VelocityEastDeviceId { get; set; }
    public ushort VelocityDownDeviceCode { get; set; }
    public ushort VelocityDownDeviceId { get; set; }

    // Status sources (single value)
    public ushort StatusDeviceCode { get; set; }
    public ushort StatusDeviceId { get; set; }
    public uint StatusValue { get { lock (m_LockObject) { return m_StatusValue; } } set { lock (m_LockObject) { m_StatusValue = value; } } }

    // Course sources (single value)
    public ushort CourseDeviceCode { get; set; }
    public ushort CourseDeviceId { get; set; }

    // Position (values)
    public WGS84Data Position { get { lock (m_LockObject) { return m_Position.Clone(); } } set { lock (m_LockObject) { m_Position = value.Clone(); } } }
    public WGS84Data Position_Deg { get { lock (m_LockObject) { var p = m_Position.Clone(); p.ConvertToDegrees(); return p; } } }

    // Euler (values)
    public EulerData EulerData { get { lock (m_LockObject) { return m_EulerData.Clone(); } } set { lock (m_LockObject) { m_EulerData = value.Clone(); } } }
    public EulerData EulerData_Deg { get { lock (m_LockObject) { var e = m_EulerData.Clone(); e.ConvertToDegrees(); return e; } } }

    // Velocity (values)
    public NEDData VelocityVector { get { lock (m_LockObject) { return m_NedVelocity.Clone(); } } set { lock (m_LockObject) { m_NedVelocity = value.Clone(); } } }
    public double VelocityTotal { get { lock (m_LockObject) { return Math.Sqrt(m_NedVelocity.Down * m_NedVelocity.Down + m_NedVelocity.East * m_NedVelocity.East + m_NedVelocity.North * m_NedVelocity.North); } } }

    // Course (value)
    public double Course { get { lock (m_LockObject) { return m_Course; } } set { lock (m_LockObject) { m_Course = value; } } }

    // Binary payload length for CommFrame Encode/Decode
    public static int BinLength
    {
        get
        {
            // OutputTime: DeviceCode(ushort) + DeviceId(ushort) + DateTimeBinary(long)
            // 14 numeric fields: each DeviceCode(ushort) + DeviceId(ushort) + Value(double)
            // Status: DeviceCode(ushort) + DeviceId(ushort) + Value(uint)
            return (2 * sizeof(ushort) + sizeof(long)) + (14 * (2 * sizeof(ushort) + sizeof(double))) + (2 * sizeof(ushort) + sizeof(uint));
        }
    }

    #endregion

    #region Constructors

    // Creates a new integrated output data instance
    public IntegratedInsOutput_Data()
    {
        m_LockObject = new object();
        m_Position = new WGS84Data();
        m_EulerData = new EulerData();
        m_NedVelocity = new NEDData();
        Clear();
    }

    #endregion

    #region Functions

    // Resets values and sources to defaults
    public void Clear()
    {
        lock (m_LockObject)
        {
            OutputTimeDeviceCode = 0; OutputTimeDeviceId = 0; m_OutputTime = DateTime.UtcNow;

            LatitudeDeviceCode = 0; LatitudeDeviceId = 0;
            LongitudeDeviceCode = 0; LongitudeDeviceId = 0;
            AltitudeDeviceCode = 0; AltitudeDeviceId = 0;

            RollDeviceCode = 0; RollDeviceId = 0;
            PitchDeviceCode = 0; PitchDeviceId = 0;
            AzimuthDeviceCode = 0; AzimuthDeviceId = 0;

            RollRateDeviceCode = 0; RollRateDeviceId = 0;
            PitchRateDeviceCode = 0; PitchRateDeviceId = 0;
            AzimuthRateDeviceCode = 0; AzimuthRateDeviceId = 0;

            VelocityTotalDeviceCode = 0; VelocityTotalDeviceId = 0;
            VelocityNorthDeviceCode = 0; VelocityNorthDeviceId = 0;
            VelocityEastDeviceCode = 0; VelocityEastDeviceId = 0;
            VelocityDownDeviceCode = 0; VelocityDownDeviceId = 0;

            StatusDeviceCode = 0; StatusDeviceId = 0; m_StatusValue = 0;

            CourseDeviceCode = 0; CourseDeviceId = 0; m_Course = 0;

            m_Position.Clear();
            m_EulerData.Clear();
            m_NedVelocity.Clear();
        }
    }

    // Creates a deep copy of the instance
    public IntegratedInsOutput_Data Clone()
    {
        var outValue = new IntegratedInsOutput_Data();
        lock (m_LockObject)
        {
            outValue.OutputTimeDeviceCode = OutputTimeDeviceCode;
            outValue.OutputTimeDeviceId = OutputTimeDeviceId;
            outValue.m_OutputTime = m_OutputTime;

            outValue.LatitudeDeviceCode = LatitudeDeviceCode; outValue.LatitudeDeviceId = LatitudeDeviceId;
            outValue.LongitudeDeviceCode = LongitudeDeviceCode; outValue.LongitudeDeviceId = LongitudeDeviceId;
            outValue.AltitudeDeviceCode = AltitudeDeviceCode; outValue.AltitudeDeviceId = AltitudeDeviceId;

            outValue.RollDeviceCode = RollDeviceCode; outValue.RollDeviceId = RollDeviceId;
            outValue.PitchDeviceCode = PitchDeviceCode; outValue.PitchDeviceId = PitchDeviceId;
            outValue.AzimuthDeviceCode = AzimuthDeviceCode; outValue.AzimuthDeviceId = AzimuthDeviceId;

            outValue.RollRateDeviceCode = RollRateDeviceCode; outValue.RollRateDeviceId = RollRateDeviceId;
            outValue.PitchRateDeviceCode = PitchRateDeviceCode; outValue.PitchRateDeviceId = PitchRateDeviceId;
            outValue.AzimuthRateDeviceCode = AzimuthRateDeviceCode; outValue.AzimuthRateDeviceId = AzimuthRateDeviceId;

            outValue.VelocityTotalDeviceCode = VelocityTotalDeviceCode; outValue.VelocityTotalDeviceId = VelocityTotalDeviceId;
            outValue.VelocityNorthDeviceCode = VelocityNorthDeviceCode; outValue.VelocityNorthDeviceId = VelocityNorthDeviceId;
            outValue.VelocityEastDeviceCode = VelocityEastDeviceCode; outValue.VelocityEastDeviceId = VelocityEastDeviceId;
            outValue.VelocityDownDeviceCode = VelocityDownDeviceCode; outValue.VelocityDownDeviceId = VelocityDownDeviceId;

            outValue.StatusDeviceCode = StatusDeviceCode; outValue.StatusDeviceId = StatusDeviceId; outValue.m_StatusValue = m_StatusValue;

            outValue.CourseDeviceCode = CourseDeviceCode; outValue.CourseDeviceId = CourseDeviceId; outValue.m_Course = m_Course;

            outValue.m_Position = m_Position.Clone();
            outValue.m_EulerData = m_EulerData.Clone();
            outValue.m_NedVelocity = m_NedVelocity.Clone();
        }
        return outValue;
    }

    // Reads the binary payload in the locked field order
    public void ReadBinary(BinaryReader i_Reader)
    {
        lock (m_LockObject)
        {
            OutputTimeDeviceCode = i_Reader.ReadUInt16();
            OutputTimeDeviceId = i_Reader.ReadUInt16();
            m_OutputTime = DateTime.FromBinary(i_Reader.ReadInt64());

            LatitudeDeviceCode = i_Reader.ReadUInt16();
            LatitudeDeviceId = i_Reader.ReadUInt16();
            m_Position.Lat = i_Reader.ReadDouble();

            LongitudeDeviceCode = i_Reader.ReadUInt16();
            LongitudeDeviceId = i_Reader.ReadUInt16();
            m_Position.Lon = i_Reader.ReadDouble();

            AltitudeDeviceCode = i_Reader.ReadUInt16();
            AltitudeDeviceId = i_Reader.ReadUInt16();
            m_Position.Alt = i_Reader.ReadDouble();

            RollDeviceCode = i_Reader.ReadUInt16();
            RollDeviceId = i_Reader.ReadUInt16();
            m_EulerData.Angles.Roll = i_Reader.ReadDouble();

            PitchDeviceCode = i_Reader.ReadUInt16();
            PitchDeviceId = i_Reader.ReadUInt16();
            m_EulerData.Angles.Pitch = i_Reader.ReadDouble();

            AzimuthDeviceCode = i_Reader.ReadUInt16();
            AzimuthDeviceId = i_Reader.ReadUInt16();
            m_EulerData.Angles.Yaw = i_Reader.ReadDouble();

            RollRateDeviceCode = i_Reader.ReadUInt16();
            RollRateDeviceId = i_Reader.ReadUInt16();
            m_EulerData.Rates.Roll = i_Reader.ReadDouble();

            PitchRateDeviceCode = i_Reader.ReadUInt16();
            PitchRateDeviceId = i_Reader.ReadUInt16();
            m_EulerData.Rates.Pitch = i_Reader.ReadDouble();

            AzimuthRateDeviceCode = i_Reader.ReadUInt16();
            AzimuthRateDeviceId = i_Reader.ReadUInt16();
            m_EulerData.Rates.Yaw = i_Reader.ReadDouble();

            VelocityTotalDeviceCode = i_Reader.ReadUInt16();
            VelocityTotalDeviceId = i_Reader.ReadUInt16();
            var velocityTotal = i_Reader.ReadDouble(); // Stored as a field even if derivable

            VelocityNorthDeviceCode = i_Reader.ReadUInt16();
            VelocityNorthDeviceId = i_Reader.ReadUInt16();
            m_NedVelocity.North = i_Reader.ReadDouble();

            VelocityEastDeviceCode = i_Reader.ReadUInt16();
            VelocityEastDeviceId = i_Reader.ReadUInt16();
            m_NedVelocity.East = i_Reader.ReadDouble();

            VelocityDownDeviceCode = i_Reader.ReadUInt16();
            VelocityDownDeviceId = i_Reader.ReadUInt16();
            m_NedVelocity.Down = i_Reader.ReadDouble();

            StatusDeviceCode = i_Reader.ReadUInt16();
            StatusDeviceId = i_Reader.ReadUInt16();
            m_StatusValue = i_Reader.ReadUInt32();

            CourseDeviceCode = i_Reader.ReadUInt16();
            CourseDeviceId = i_Reader.ReadUInt16();
            m_Course = i_Reader.ReadDouble();

            // Keep the stored total as-is if you later want to expose it directly
            _ = velocityTotal;
        }
    }

    // Encodes the binary payload in the locked field order
    public void Encode(BinaryWriter i_Writer)
    {
        lock (m_LockObject)
        {
            i_Writer.Write(OutputTimeDeviceCode);
            i_Writer.Write(OutputTimeDeviceId);
            i_Writer.Write(m_OutputTime.ToBinary());

            i_Writer.Write(LatitudeDeviceCode);
            i_Writer.Write(LatitudeDeviceId);
            i_Writer.Write(m_Position.Lat);

            i_Writer.Write(LongitudeDeviceCode);
            i_Writer.Write(LongitudeDeviceId);
            i_Writer.Write(m_Position.Lon);

            i_Writer.Write(AltitudeDeviceCode);
            i_Writer.Write(AltitudeDeviceId);
            i_Writer.Write(m_Position.Alt);

            i_Writer.Write(RollDeviceCode);
            i_Writer.Write(RollDeviceId);
            i_Writer.Write(m_EulerData.Angles.Roll);

            i_Writer.Write(PitchDeviceCode);
            i_Writer.Write(PitchDeviceId);
            i_Writer.Write(m_EulerData.Angles.Pitch);

            i_Writer.Write(AzimuthDeviceCode);
            i_Writer.Write(AzimuthDeviceId);
            i_Writer.Write(m_EulerData.Angles.Yaw);

            i_Writer.Write(RollRateDeviceCode);
            i_Writer.Write(RollRateDeviceId);
            i_Writer.Write(m_EulerData.Rates.Roll);

            i_Writer.Write(PitchRateDeviceCode);
            i_Writer.Write(PitchRateDeviceId);
            i_Writer.Write(m_EulerData.Rates.Pitch);

            i_Writer.Write(AzimuthRateDeviceCode);
            i_Writer.Write(AzimuthRateDeviceId);
            i_Writer.Write(m_EulerData.Rates.Yaw);

            i_Writer.Write(VelocityTotalDeviceCode);
            i_Writer.Write(VelocityTotalDeviceId);
            i_Writer.Write(VelocityTotal); // Stored total

            i_Writer.Write(VelocityNorthDeviceCode);
            i_Writer.Write(VelocityNorthDeviceId);
            i_Writer.Write(m_NedVelocity.North);

            i_Writer.Write(VelocityEastDeviceCode);
            i_Writer.Write(VelocityEastDeviceId);
            i_Writer.Write(m_NedVelocity.East);

            i_Writer.Write(VelocityDownDeviceCode);
            i_Writer.Write(VelocityDownDeviceId);
            i_Writer.Write(m_NedVelocity.Down);

            i_Writer.Write(StatusDeviceCode);
            i_Writer.Write(StatusDeviceId);
            i_Writer.Write(m_StatusValue);

            i_Writer.Write(CourseDeviceCode);
            i_Writer.Write(CourseDeviceId);
            i_Writer.Write(m_Course);
        }
    }

    #endregion
}
