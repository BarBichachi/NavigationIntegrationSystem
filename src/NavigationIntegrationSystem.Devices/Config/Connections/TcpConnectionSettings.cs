namespace NavigationIntegrationSystem.Devices.Config.Connections;

// TCP connection parameters
public sealed class TcpConnectionSettings
{
    #region Properties
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    #endregion

    #region Functions
    // Copies values from another instance
    public void CopyFrom(TcpConnectionSettings i_Source)
    {
        if (i_Source == null) { return; }
        Host = i_Source.Host;
        Port = i_Source.Port;
    }
    #endregion
}