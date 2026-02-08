using Infrastructure.Serialization;
using Infrastructure.Tools;

using Microsoft.VisualBasic;

using System;
using System.IO;

using Constants = Infrastructure.Tools.Constants;

namespace Infrastructure.DataStructures;

public class WGS84Data
{
    #region Properties
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double Alt { get; set; }
    #endregion

    #region Constructors
    // Default constructor
    public WGS84Data() { Clear(); }

    // Decode constructor matching VIC patterns
    public WGS84Data(BinaryReader i_Reader)
    {
        Lat = CommunicationBase.DecodeGeneralDoubleValue(i_Reader);
        Lon = CommunicationBase.DecodeGeneralDoubleValue(i_Reader);
        Alt = CommunicationBase.DecodeGeneralDoubleValue(i_Reader);
    }
    #endregion

    #region Functions
    // Resets values to zero
    public void Clear()
    {
        Lat = Lon = Alt = 0.0;
    }

    // Creates a deep copy
    public WGS84Data Clone()
    {
        return (WGS84Data)this.MemberwiseClone();
    }

    // Converts radians to degrees
    public void ConvertToDegrees()
    {
        Lat *= Constants.RadToDegRatio;
        Lon *= Constants.RadToDegRatio;
    }

    // Converts degrees to radians
    public void ConvertToRadians()
    {
        Lat *= Constants.DegToRadRatio;
        Lon *= Constants.DegToRadRatio;
    }

    // Binary encoding matching VIC patterns
    public void Encode(BinaryWriter i_Writer)
    {
        i_Writer.Write(CommunicationBase.EncodeGeneralDoubleValue(Lat));
        i_Writer.Write(CommunicationBase.EncodeGeneralDoubleValue(Lon));
        i_Writer.Write(CommunicationBase.EncodeGeneralDoubleValue(Alt));
    }
    #endregion
}