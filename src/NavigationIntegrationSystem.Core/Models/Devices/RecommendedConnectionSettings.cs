namespace NavigationIntegrationSystem.Core.Models.Devices;

// Optional per-device-definition hints displayed above connection-setting inputs in the UI. Each property is null when no hint applies; the UI's RecommendedHint control collapses cleanly on null/empty so views don't have to branch
public sealed class RecommendedConnectionSettings
{
    #region Properties
    public string? KindHint { get; init; }
    public string? ComPortHint { get; init; }
    public string? BaudRateHint { get; init; }
    public string? RemoteIpHint { get; init; }
    public string? RemotePortHint { get; init; }
    public string? LocalIpHint { get; init; }
    public string? LocalPortHint { get; init; }
    #endregion
}
