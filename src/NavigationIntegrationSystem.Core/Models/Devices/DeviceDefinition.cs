using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Models.DeviceCatalog;
using System.Collections.Generic;

namespace NavigationIntegrationSystem.Core.Models.Devices;

// Defines a fixed device instance in the app, including its inspectable fields
public sealed class DeviceDefinition
{
    #region Properties
    public DeviceType Type { get; }
    public string DisplayName => Type.ToString();
    public IReadOnlyList<DeviceFieldDefinition> Fields { get; }
    // Optional hints rendered above connection-setting inputs in the device settings pane. Null when the device has nothing to recommend
    public RecommendedConnectionSettings? RecommendedConnection { get; }
    #endregion

    #region Ctors
    public DeviceDefinition(DeviceType i_Type, IReadOnlyList<DeviceFieldDefinition> i_Fields, RecommendedConnectionSettings? i_RecommendedConnection = null)
    {
        Type = i_Type;
        Fields = i_Fields;
        RecommendedConnection = i_RecommendedConnection;
    }
    #endregion
}