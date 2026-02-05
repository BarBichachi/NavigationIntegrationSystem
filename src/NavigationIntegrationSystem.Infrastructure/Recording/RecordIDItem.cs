using System.IO;

namespace NavigationIntegrationSystem.Infrastructure.Recording;

public class RecordIDItem
{
    public string recordIDName { get; set; } = string.Empty;
    public bool isChecked { get; set; }
    public string rawDataLine { get; set; } = string.Empty;

    // Changed to nullable as a file might not be open at instantiation
    public StreamWriter? rawDataFile { get; set; }

    public RecordIDItem()
    {
        isChecked = true;
    }
}