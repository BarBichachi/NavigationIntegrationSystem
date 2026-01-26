namespace NavigationIntegrationSystem.Core.Models.DeviceCatalog;

// Defines a single field that can be displayed in Inspect for a device
public sealed class DeviceFieldDefinition
{
    #region Properties
    public string Key { get; }
    public string DisplayName { get; }
    public string Unit { get; }
    #endregion

    #region Ctors
    public DeviceFieldDefinition(string i_Key, string i_DisplayName, string i_Unit)
    {
        Key = i_Key;
        DisplayName = i_DisplayName;
        Unit = i_Unit;
    }
    #endregion
}