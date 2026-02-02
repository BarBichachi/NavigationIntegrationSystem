using System;
using System.IO;

namespace Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;

// Unified status flags for IntegratedInsOutput_Data StatusValue (bitmask)
[Flags]
public enum IntegratedInsOutputStatusFlags : uint
{
    None = 0,

    // Time
    OutputTimeValid = 1u << 0,

    // Position
    PositionLatValid = 1u << 1,
    PositionLonValid = 1u << 2,
    PositionAltValid = 1u << 3,

    // Euler Angles
    RollValid = 1u << 4,
    PitchValid = 1u << 5,
    AzimuthValid = 1u << 6,

    // Euler Rates
    RollRateValid = 1u << 7,
    PitchRateValid = 1u << 8,
    AzimuthRateValid = 1u << 9,

    // Velocity
    VelocityTotalValid = 1u << 10,
    VelocityNorthValid = 1u << 11,
    VelocityEastValid = 1u << 12,
    VelocityDownValid = 1u << 13,

    // Course
    CourseValid = 1u << 14,

    // Generic / future
    Reserved15 = 1u << 15
}