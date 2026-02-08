using System;

namespace Infrastructure.Tools;

public static class Constants
{
    public const double DegToRadRatio = Math.PI / 180.0;
    public const double RadToDegRatio = 180.0 / Math.PI;
}

public static class MathTools
{
    // Normalizes angle to [0, 2PI)
    public static double Cyclic2PI(double i_Angle)
    {
        double val = i_Angle % (2.0 * Math.PI);
        if (val < 0) val += (2.0 * Math.PI);
        return val;
    }
}