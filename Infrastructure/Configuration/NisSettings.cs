namespace NavigationIntegrationSystem.Infrastructure.Configuration;

// Holds application-wide settings for NIS
public sealed class NisSettings
{
    public LogSettings Log { get; set; } = new LogSettings();
}