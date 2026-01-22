using NavigationIntegrationSystem.Core.Enums;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Core.Models;

// Defines a fixed device instance in the app, including its inspectable fields
public sealed class DeviceDefinition
{
    #region Properties
    public DeviceType Type { get; }
    public string DisplayName => Type.ToString();
    public IReadOnlyList<DeviceFieldDefinition> Fields { get; }
    #endregion

    #region Ctors
    public DeviceDefinition(DeviceType i_Type, IReadOnlyList<DeviceFieldDefinition> i_Fields)
    {
        Type = i_Type;
        Fields = i_Fields;
    }
    #endregion
}