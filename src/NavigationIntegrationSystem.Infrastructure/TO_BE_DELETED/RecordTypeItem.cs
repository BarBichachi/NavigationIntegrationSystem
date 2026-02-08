using NavigationIntegrationSystem.Infrastructure.Recording;

using System.Collections.Generic;
namespace RecordDecoderPro.ItemTemplates
{
    public class RecordTypeItem : ViewModelBase
    {

        public string recordTypeName { get; set; }
        public List<RecordIDItem> recordIDs { get; set; }
        public bool isChecked { get; set; }
        public List<RecordFieldItem> recordFields { get; set; }
        public Dictionary<string, string> dict { get; set; }

        public RecordTypeItem()
        {
            recordIDs = new List<RecordIDItem>();
            recordFields = new List<RecordFieldItem>();
            isChecked = true;
        }

        private string[] columnsNames { get; set; }
        public string[] ColumnsNames
        {
            get { return columnsNames; }
            set
            {
                columnsNames = value;
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
}
