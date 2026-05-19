using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;

namespace NavigationIntegrationSystem.UI.Services.Recording;

// Immutable per-row snapshot consumed by the background recording loop.
// All fields are value-types or already-immutable references, so reads/writes are thread-safe without further synchronization.
public readonly record struct IntegrationRowSnapshot(
    string FieldName,
    bool HasSelection,
    DeviceType DeviceType,
    IInsDevice? SourceDevice,
    double Value);
