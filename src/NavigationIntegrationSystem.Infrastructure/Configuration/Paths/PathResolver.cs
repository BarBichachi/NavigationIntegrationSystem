using System;

namespace NavigationIntegrationSystem.Infrastructure.Configuration.Paths;

// Resolves special path tokens like {BaseDirectory} to runtime paths
public static class PathResolver
{
    // Expands supported tokens into concrete runtime paths
    public static string Resolve(string i_Path)
    {
        string baseDir = AppContext.BaseDirectory.TrimEnd('\\');

        if (string.IsNullOrWhiteSpace(i_Path)) { return baseDir; }

        return i_Path.Replace("{BaseDirectory}", baseDir);
    }

}