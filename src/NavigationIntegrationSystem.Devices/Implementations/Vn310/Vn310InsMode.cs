namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// VN310 INS operating mode. Values match the low 2 bits of the InsStatus word emitted by the sensor.
public enum Vn310InsMode
{
    NotTracking = 0,
    Aligning = 1,
    Tracking = 2,
    GnssLoss = 3
}
