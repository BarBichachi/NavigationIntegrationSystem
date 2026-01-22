namespace NavigationIntegrationSystem.Devices.Config.Connections;

// TCP connection parameters
public sealed class TcpConnectionConfig
{
    #region Properties
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    #endregion

    #region Functions
    // Copies values from another instance
    public void CopyFrom(TcpConnectionConfig i_Source)
    {
        if (i_Source == null) { return; }
        Host = i_Source.Host;
        Port = i_Source.Port;
    }

    // Creates a deep clone of this instance
    public TcpConnectionConfig DeepClone()
    {
        var clone = new TcpConnectionConfig();
        clone.CopyFrom(this);
        return clone;
    }
    #endregion
}