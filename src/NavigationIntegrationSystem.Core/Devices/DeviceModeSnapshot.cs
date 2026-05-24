namespace NavigationIntegrationSystem.Core.Devices;

public sealed class DeviceModeSnapshot
{
    public string Label { get; init; } = string.Empty;
    public DeviceModeSeverity Severity { get; init; }
}
