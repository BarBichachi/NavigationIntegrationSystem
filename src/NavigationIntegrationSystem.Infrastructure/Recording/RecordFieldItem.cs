namespace NavigationIntegrationSystem.Infrastructure.Recording;

public class RecordFieldItem : ViewModelBase
{
    public string recordFieldName { get; set; } = string.Empty;

    public RecordFieldItem()
    {
        isChecked = true;
    }

    private bool isChecked;

    public bool IsChecked
    {
        get { return isChecked; }
        set
        {
            isChecked = value;
            OnPropertyChanged();
        }
    }
}