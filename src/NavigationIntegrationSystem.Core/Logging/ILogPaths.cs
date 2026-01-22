namespace NavigationIntegrationSystem.Core.Logging;

// Exposes log-related paths to UI without referencing infrastructure types
public interface ILogPaths
{
    string LogFolderPath { get; }
}
