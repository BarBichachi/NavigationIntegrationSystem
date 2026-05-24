namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// Which wire format the most recent packet was decoded from. Drives the ASCII-mode banner in the VN310 inspect view (which warns that angular rates are 0 because ASCII VNINS doesn't carry them) and the binary-mode banner (which notes that uncertainties are 0 because our subscribed binary groups don't include AttitudeGroup/InsGroup)
public enum Vn310PacketSourceMode
{
    Unknown = 0,
    Ascii,
    Binary
}
