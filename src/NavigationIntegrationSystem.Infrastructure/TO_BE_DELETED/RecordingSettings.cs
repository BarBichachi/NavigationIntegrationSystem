using System.Collections.Generic;

namespace Infrastructure.FileManagement.Settings
{
    public class RecordingSettings
    {
        public List<string> ScenariosList { get; set; } = new List<string>();
        public ulong MaximumFileSize { get; set; } = 1024 * 1024 * 10; // 10MB default

        public void LoadDefaultSettings()
        {
            ScenariosList = new List<string> { "DefaultScenario" };
            MaximumFileSize = 1024 * 1024 * 10;
        }
    }
}