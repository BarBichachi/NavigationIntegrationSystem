namespace NavigationIntegrationSystem.Infrastructure.Configuration.Devices;

// Holds persisted connection parameters for a device using a simple discriminator model
public sealed class DeviceConnectionConfig
{
    #region Properties
    public DeviceConnectionKind Kind { get; set; } = DeviceConnectionKind.Udp;

    public string RemoteIp { get; set; } = "127.0.0.1";
    public int RemotePort { get; set; } = 5000;

    public string LocalIp { get; set; } = "0.0.0.0";
    public int LocalPort { get; set; } = 5000;

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;

    public SerialLineKind SerialLineKind { get; set; } = SerialLineKind.Rs232;
    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    #endregion
}