using System.IO;

namespace NavigationIntegrationSystem.Infrastructure.Recording;

public class RecordIDItem
{
    public string recordIDName { get; set; }
    public bool isChecked { get; set; }
    public string rawDataLine { get; set; }
    public StreamWriter rawDataFile { get; set; }

    public RecordIDItem()
    {
        isChecked = true;
    }
}