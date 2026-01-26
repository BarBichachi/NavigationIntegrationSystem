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
    #endregion

    #region Events
    event EventHandler? StateChanged;
    #endregion

    #region Functions
    // Connects to the device
    Task ConnectAsync();

    // Disconnects from the device
    Task DisconnectAsync();
    #endregion
}