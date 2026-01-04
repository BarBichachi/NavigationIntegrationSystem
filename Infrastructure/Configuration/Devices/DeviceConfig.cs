using CommunityToolkit.Mvvm.ComponentModel;

namespace NavigationIntegrationSystem.Infrastructure.Configuration.Devices;

// Holds persisted configuration for a single device instance
public sealed class DeviceConfig : ObservableObject
{
    #region Private Fields
    private string m_DeviceId = string.Empty;
    private bool m_AutoReconnect = true;
    private DeviceConnectionConfig m_Connection = new DeviceConnectionConfig();
    #endregion

    #region Properties
    public string DeviceId { get => m_DeviceId; set => SetProperty(ref m_DeviceId, value); }
    public bool AutoReconnect { get => m_AutoReconnect; set => SetProperty(ref m_AutoReconnect, value); }
    public DeviceConnectionConfig Connection { get => m_Connection; set => SetProperty(ref m_Connection, value); }
    #endregion

    #region Functions
    // Copies values from another config (deep)
    public void CopyFrom(DeviceConfig i_Source)
    {
        if (i_Source == null)
        { return; }

        DeviceId = i_Source.DeviceId;
        AutoReconnect = i_Source.AutoReconnect;

        if (Connection == null)
        { Connection = new DeviceConnectionConfig(); }
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