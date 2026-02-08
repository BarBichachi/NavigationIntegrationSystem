using System;
using System.IO;

namespace Infrastructure.Serialization;

public enum Coding
{
    Angle,
    Motion,
    General
}

public static class CommunicationBase
{
    // Mimics VIC binary encoding/decoding for doubles (simple pass-through for NIS testing)
    public static double DecodeAngle(BinaryReader i_Reader) => i_Reader.ReadDouble();
    public static double DecodeMotion(BinaryReader i_Reader) => i_Reader.ReadDouble();
    public static double DecodeGeneralDoubleValue(BinaryReader i_Reader) => i_Reader.ReadDouble();

    public static double EncodeAngle(double i_Value) => i_Value;
    public static double EncodeMotion(double i_Value) => i_Value;
    public static double EncodeGeneralDoubleValue(double i_Value) => i_Value;
}