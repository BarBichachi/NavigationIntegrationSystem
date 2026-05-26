namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// Composition of currently-fresh VN310 wire sources. Computed at merge time from per-source freshness against the staleness window. Drives the inspect view's source-mode banner and field-availability notes
public enum Vn310PacketSourceMode
{
    // No packet from either source has arrived yet (initial state) OR both sources are stale
    Unknown = 0,

    // Only ASCII VNINS is currently fresh -- angular rates and TimeStatus unavailable
    AsciiOnly,

    // Only Binary (CommonGroup + TimeGroup) is currently fresh -- AttU / PosU / VelU unavailable
    BinaryOnly,

    // Both ASCII and Binary are currently fresh -- all fields live
    Both
}
