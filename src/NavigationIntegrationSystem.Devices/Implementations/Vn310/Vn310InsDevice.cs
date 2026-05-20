using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models.Devices;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.Devices.Runtime;

using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// Runtime device implementation for VN310. Owns a Vn310TelemetryService for its lifetime and forwards lifecycle calls to it. Always uses Config.Connection.Serial regardless of Connection.Kind; VN310 is a serial-only sensor.
public sealed class Vn310InsDevice : InsDeviceBase
{
    #region Private Fields
    private readonly Vn310TelemetryService m_Service;
    // Per-connect token; cancelled in OnDisconnectAsync so a Disconnect during the SDK's ~1s settle delay short-circuits the in-flight Connect
    private CancellationTokenSource? m_ConnectCts;
    private readonly object m_CtsLock = new object();
    #endregion

    #region Properties
    public Vn310Telemetry? LatestTelemetry => m_Service.LatestTelemetry;
    #endregion

    #region Events
    // Forwarded from the inner telemetry service so consumers don't need to know it exists
    public event EventHandler<Vn310Telemetry>? TelemetryUpdated;
    #endregion

    #region Constructors
    public Vn310InsDevice(DeviceDefinition i_Definition, DeviceConfig i_Config, ILogService i_LogService)
        : base(i_Definition, i_Config, i_LogService)
    {
        m_Service = new Vn310TelemetryService(i_LogService);
    }
    #endregion

    #region Functions
    // Opens the serial port via the telemetry service. Throws on misconfiguration or SDK failure; the base class converts to DeviceStatus.Error
    protected override async Task OnConnectAsync()
    {
        string desiredPort = Config.Connection.Serial.ComPort;

        if (string.IsNullOrWhiteSpace(desiredPort))
        {
            throw new InvalidOperationException("No COM port configured for VN310.");
        }

        // Pre-flight the port name in user code so the SDK's FileNotFoundException (which originates inside VectorNav.dll and trips VS first-chance breaks) never fires for the common "configured port doesn't exist" case. Phase 5's dropdown will make this near-impossible to hit anyway
        if (!SerialPort.GetPortNames().Contains(desiredPort, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"COM port '{desiredPort}' is not available.");
        }

        CancellationToken token;
        lock (m_CtsLock)
        {
            m_ConnectCts?.Dispose();
            m_ConnectCts = new CancellationTokenSource();
            token = m_ConnectCts.Token;
        }

        // Subscribe before starting so we don't miss the first packet that arrives while StartAsync is still settling
        m_Service.TelemetryUpdated += OnServiceTelemetryUpdated;
        m_Service.Stalled += OnServiceStalled;

        try
        {
            await m_Service.StartAsync(Config.Connection.Serial.ComPort, Config.Connection.Serial.BaudRate, token).ConfigureAwait(false);
        }
        catch
        {
            // Roll back the subscriptions so we don't leak handler refs into a service that never started
            m_Service.TelemetryUpdated -= OnServiceTelemetryUpdated;
            m_Service.Stalled -= OnServiceStalled;
            throw;
        }
    }

    // Closes the serial port and detaches handlers. Unsubscribe FIRST so a watchdog Stalled callback can't race in and call SetStatus(Error) after we've already started tearing down
    protected override async Task OnDisconnectAsync()
    {
        m_Service.TelemetryUpdated -= OnServiceTelemetryUpdated;
        m_Service.Stalled -= OnServiceStalled;

        lock (m_CtsLock)
        {
            m_ConnectCts?.Cancel();
            m_ConnectCts?.Dispose();
            m_ConnectCts = null;
        }

        await m_Service.StopAsync().ConfigureAwait(false);
    }
    #endregion

    #region Event Handlers
    // Re-raises the service's telemetry event under this device's identity. Wrapped so a buggy subscriber can't surface as a packet-parsing failure inside the service
    private void OnServiceTelemetryUpdated(object? i_Sender, Vn310Telemetry i_Telemetry)
    {
        try
        {
            TelemetryUpdated?.Invoke(this, i_Telemetry);
        }
        catch
        {
            // Service already logs subscriber exceptions on its own event; nothing useful to add here
        }
    }

    // Watchdog fired: no telemetry for 2s. Map to DeviceStatus.Error so the UI surfaces it; don't auto-disconnect (user decides whether to retry)
    private void OnServiceStalled(object? i_Sender, EventArgs i_Args)
    {
        SetStatus(DeviceStatus.Error, "No telemetry for 2s");
    }
    #endregion
}
