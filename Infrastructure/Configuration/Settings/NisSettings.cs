namespace NavigationIntegrationSystem.Infrastructure.Configuration.Settings;

// Holds application-wide settings for NIS
public sealed class NisSettings
{
    public LogSettings Log { get; set; } = new LogSettings();
}