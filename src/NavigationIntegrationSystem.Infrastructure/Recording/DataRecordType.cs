using System;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Infrastructure.Recording;

public class RecordTypeItem : ViewModelBase
{
    public string recordTypeName { get; set; } = string.Empty;
    public List<RecordIDItem> recordIDs { get; set; }
    public bool isChecked { get; set; }
    public List<RecordFieldItem> recordFields { get; set; }
    public Dictionary<string, string> dict { get; set; } = new();

    public RecordTypeItem()
    {
        recordIDs = new List<RecordIDItem>();
        recordFields = new List<RecordFieldItem>();
        isChecked = true;
        // dict initialized inline above
    }

    private string[] columnsNames { get; set; } = Array.Empty<string>();

    public string[] ColumnsNames
    {
        get { return columnsNames; }
        set
        {
            columnsNames = value ?? Array.Empty<string>();
            foreach (string name in columnsNames)
            {
                recordFields.Add(new RecordFieldItem() { recordFieldName = name });
            }
        }
    }

    public RecordTypeItem Clone()
    {
        return (RecordTypeItem)MemberwiseClone();
    }
}