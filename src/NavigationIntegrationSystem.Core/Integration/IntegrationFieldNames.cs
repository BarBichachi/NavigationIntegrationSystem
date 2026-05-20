namespace NavigationIntegrationSystem.Core.Integration;

// Canonical names for integration grid rows. UI labels and switch arms across the snapshot pipeline reference these constants instead of duplicated literals.
public static class IntegrationFieldNames
{
    public const string Latitude = "Latitude";
    public const string Longitude = "Longitude";
    public const string Altitude = "Altitude";

    public const string Yaw = "Yaw";
    public const string Pitch = "Pitch";
    public const string Roll = "Roll";

    public const string YawRate = "Yaw Rate";
    public const string PitchRate = "Pitch Rate";
    public const string RollRate = "Roll Rate";

    public const string VelocityNorth = "Velocity North";
    public const string VelocityEast = "Velocity East";
    public const string VelocityDown = "Velocity Down";
    public const string VelocityTotal = "Velocity Total";

    public const string Course = "Course";
}
