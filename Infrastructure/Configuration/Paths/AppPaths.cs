using System;
using System.IO;

namespace NavigationIntegrationSystem.Infrastructure.Configuration.Paths;

// Centralizes application file paths under the output directory
public static class AppPaths
{
    #region Properties
    public static string BaseDirectory => AppContext.BaseDirectory.TrimEnd('\\');
    public static string ConfigDirectory => Path.Combine(BaseDirectory, "Config");
    public static string DevicesConfigPath => Path.Combine(ConfigDirectory, "devices.json");
    #endregion
}
