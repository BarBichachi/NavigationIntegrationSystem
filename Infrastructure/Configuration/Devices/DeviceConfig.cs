namespace NavigationIntegrationSystem.Infrastructure.Configuration.Devices;

// Holds persisted configuration for a single device instance
public sealed class DeviceConfig
{
    #region Properties
    public string DeviceId { get; set; } = string.Empty;
    public bool AutoReconnect { get; set; } = true;
    public DeviceConnectionConfig Connection { get; set; } = new DeviceConnectionConfig();
    #endregion
}