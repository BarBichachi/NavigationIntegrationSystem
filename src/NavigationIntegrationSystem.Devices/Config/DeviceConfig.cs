using NavigationIntegrationSystem.Core.Enums;

namespace NavigationIntegrationSystem.Devices.Config;

// Holds persisted configuration for a single device instance
public sealed class DeviceConfig
{
    #region Properties
    public DeviceType DeviceType { get; set; }
    public bool AutoReconnect { get; set; } = true;
    public DeviceConnectionConfig Connection { get; set; } = new DeviceConnectionConfig();
    #endregion

    #region Functions
    // Copies values from another config (deep)
    public void CopyFrom(DeviceConfig i_Source)
    {
        if (i_Source == null) { return; }

        DeviceType = i_Source.DeviceType;
        AutoReconnect = i_Source.AutoReconnect;

        if (Connection == null) { Connection = new DeviceConnectionConfig(); }

        Connection.CopyFrom(i_Source.Connection);
    }

    // Creates a deep clone of this config
    public DeviceConfig DeepClone()
    {
        var clone = new DeviceConfig();
        clone.CopyFrom(this);
        return clone;
    }
    #endregion
}