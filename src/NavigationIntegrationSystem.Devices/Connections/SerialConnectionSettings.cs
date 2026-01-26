using NavigationIntegrationSystem.Devices.Enums;

namespace NavigationIntegrationSystem.Devices.Connections;

// Serial connection parameters
public sealed class SerialConnectionSettings
{
    #region Properties
    public SerialLineKind SerialLineKind { get; set; } = SerialLineKind.Rs232;
    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    #endregion

    #region Functions
    // Copies values from another instance
    public void CopyFrom(SerialConnectionSettings i_Source)
    {
        if (i_Source == null) { return; }
        SerialLineKind = i_Source.SerialLineKind;
        ComPort = i_Source.ComPort;
        BaudRate = i_Source.BaudRate;
    }
    #endregion
}