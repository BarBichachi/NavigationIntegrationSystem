using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Models.Devices;
using System;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.Core.Devices;

// Defines a runtime INS device instance
public interface IInsDevice
{
    #region Properties
    DeviceDefinition Definition { get; }
    DeviceStatus Status { get; }
    string? LastError { get; }
    DeviceModeSnapshot? CurrentMode { get; }
    // True while an auto-reconnect backoff loop is active. UI uses this to show a Cancel affordance and a countdown indicator
    bool IsAutoReconnecting { get; }
    // Human-readable countdown text ("Reconnecting in 5s..." / "Reconnecting...") when a loop is active, null otherwise. Updates ~1Hz during backoff
    string? ReconnectStatusText { get; }
    #endregion

    #region Events
    event EventHandler? StateChanged;
    event EventHandler? ModeChanged;
    #endregion

    #region Functions
    // Connects to the device
    Task ConnectAsync();

    // Disconnects from the device
    Task DisconnectAsync();

    // Called by the UI when the user toggles the AutoReconnect preference. Lets the device cancel an in-flight backoff loop immediately (instead of waiting for the next loop iteration to check the flag), or start a fresh loop if turning on while in Error
    void NotifyAutoReconnectChanged();
    #endregion
}