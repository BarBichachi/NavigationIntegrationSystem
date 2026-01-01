using System.Collections.Generic;

using NavigationIntegrationSystem.Core.Enums;

namespace NavigationIntegrationSystem.Core.Models;

// Defines a fixed device instance in the app, including its inspectable fields
public sealed class DeviceDefinition
{
    #region Properties
    public string DeviceId { get; }
    public string DisplayName { get; }
    public DeviceType Type { get; }
    public IReadOnlyList<DeviceFieldDefinition> Fields { get; }
    #endregion

    #region Ctors
    public DeviceDefinition(string i_DeviceId, string i_DisplayName, DeviceType i_Type, IReadOnlyList<DeviceFieldDefinition> i_Fields)
    {
        DeviceId = i_DeviceId;
        DisplayName = i_DisplayName;
        Type = i_Type;
        Fields = i_Fields;
    }
    #endregion
}
