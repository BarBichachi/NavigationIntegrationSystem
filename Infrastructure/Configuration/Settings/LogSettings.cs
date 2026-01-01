namespace NavigationIntegrationSystem.Infrastructure.Configuration.Settings;

// Holds log settings such as file path and UI buffer limits
public sealed class LogSettings
{
    public string Root { get; set; } = "{BaseDirectory}\\Logs";
    public int MaxUiEntries { get; set; } = 2000;
}