namespace NavigationIntegrationSystem.Infrastructure.Configuration;

// Holds strongly-typed settings loaded from appsettings.json
public sealed class AppSettings
{
    public NisSettings Nis { get; set; } = new NisSettings();
}