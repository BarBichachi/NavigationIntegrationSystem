namespace NavigationIntegrationSystem.Devices.Connections;

// UDP connection parameters
public sealed class UdpConnectionSettings
{
    #region Properties
    public string RemoteIp { get; set; } = "127.0.0.1";
    public int RemotePort { get; set; } = 5000;
    public string LocalIp { get; set; } = "0.0.0.0";
    public int LocalPort { get; set; } = 5000;
    #endregion

    #region Functions
    // Copies values from another instance
    public void CopyFrom(UdpConnectionSettings i_Source)
    {
        if (i_Source == null) { return; }
        RemoteIp = i_Source.RemoteIp;
        RemotePort = i_Source.RemotePort;
        LocalIp = i_Source.LocalIp;
        LocalPort = i_Source.LocalPort;
    }
    #endregion
}
