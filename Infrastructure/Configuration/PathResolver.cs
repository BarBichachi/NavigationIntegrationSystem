using System;

namespace NavigationIntegrationSystem.Infrastructure.Configuration;

// Resolves special path tokens like {BaseDirectory} to runtime paths
public static class PathResolver
{
    // Expands supported tokens into concrete runtime paths
    public static string Resolve(string i_Path)
    {
        if (string.IsNullOrWhiteSpace(i_Path)) { return AppContext.BaseDirectory; }
        return i_Path.Replace("{BaseDirectory}", AppContext.BaseDirectory.TrimEnd('\\'));
    }
}