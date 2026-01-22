using NavigationIntegrationSystem.Devices.Config.Enums;

namespace NavigationIntegrationSystem.Devices.Config.Connections;

// Serial connection parameters
public sealed class SerialConnectionConfig
{
    #region Properties
    public SerialLineKind SerialLineKind { get; set; } = SerialLineKind.Rs232;
    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    #endregion

    #region Functions
    // Copies values from another instance
    public void CopyFrom(SerialConnectionConfig i_Source)
    {
        if (i_Source == null) { return; }
        SerialLineKind = i_Source.SerialLineKind;
        ComPort = i_Source.ComPort;
        BaudRate = i_Source.BaudRate;
    }

    // Creates a deep clone of this instance
    public SerialConnectionConfig DeepClone()
    {
        var clone = new SerialConnectionConfig();
        clone.CopyFrom(this);
        return clone;
    }
    #endregion
}