using NavigationIntegrationSystem.Core.Devices;
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
    // Cached mode snapshot + the underlying Vn310InsMode it was derived from. Compared on each packet so ModeChanged only fires on actual transitions, not per-packet (which would flood UI listeners at ~10-200Hz). Lock-protected because read/write happens on the SDK packet thread + UI thread
    private DeviceModeSnapshot? m_CurrentMode;
    private Vn310InsMode? m_LastEmittedMode;
    private readonly object m_ModeLock = new object();
    #endregion

    #region Properties
    public Vn310Telemetry? LatestTelemetry => m_Service.LatestTelemetry;
    public override DeviceModeSnapshot? CurrentMode { get { lock (m_ModeLock) { return m_CurrentMode; } } }
    // Packet stats forwarded from the inner service so the inspect VM doesn't have to reach into m_Service. Stats persist across inspect pane open/close because the service lives for this device's whole runtime lifetime
    public long PacketCount => m_Service.PacketCount;
    public Vn310PacketSourceMode LastSourceMode => m_Service.LastSourceMode;
    public DateTime[] GetRecentPacketTimestamps() => m_Service.GetRecentPacketTimestamps();
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
        ResetModeAndRaiseIfChanged();
    }

    // Maps a VN310 INS mode value to its display snapshot. Severity buckets follow the convention "Good = trustworthy outputs, Bad = nothing valid yet"
    private static DeviceModeSnapshot MapMode(Vn310InsMode i_Mode)
    {
        return i_Mode switch
        {
            Vn310InsMode.Tracking => new DeviceModeSnapshot { Label = "TRACKING", Severity = DeviceModeSeverity.Good },
            Vn310InsMode.Aligning => new DeviceModeSnapshot { Label = "ALIGNING", Severity = DeviceModeSeverity.Warning },
            Vn310InsMode.GnssLoss => new DeviceModeSnapshot { Label = "GNSS LOSS", Severity = DeviceModeSeverity.Caution },
            Vn310InsMode.NotTracking => new DeviceModeSnapshot { Label = "NOT TRACKING", Severity = DeviceModeSeverity.Bad },
            _ => new DeviceModeSnapshot { Label = "?", Severity = DeviceModeSeverity.Unknown }
        };
    }

    // Sets CurrentMode from the just-received telemetry and raises ModeChanged iff the underlying mode value differs from the last one we emitted. Called from OnServiceTelemetryUpdated on the SDK packet thread
    private void UpdateModeAndRaiseIfChanged(Vn310InsMode i_NewMode)
    {
        bool changed;
        lock (m_ModeLock)
        {
            changed = !m_LastEmittedMode.HasValue || m_LastEmittedMode.Value != i_NewMode;
            if (changed)
            {
                m_LastEmittedMode = i_NewMode;
                m_CurrentMode = MapMode(i_NewMode);
            }
        }
        if (changed) { RaiseModeChanged(); }
    }

    // Clears CurrentMode -> null and raises ModeChanged iff we previously had a mode. Called on Disconnect + Stall so the UI chip collapses (no stale "TRACKING" left over after a serial cable yank)
    private void ResetModeAndRaiseIfChanged()
    {
        bool changed;
        lock (m_ModeLock)
        {
            changed = m_LastEmittedMode.HasValue;
            if (changed)
            {
                m_LastEmittedMode = null;
                m_CurrentMode = null;
            }
        }
        if (changed) { RaiseModeChanged(); }
    }
    #endregion

    #region Event Handlers
    // Re-raises the service's telemetry event under this device's identity. Wrapped so a buggy subscriber can't surface as a packet-parsing failure inside the service. Also drives the mode-change event so the device card chip updates without needing to subscribe to TelemetryUpdated directly
    private void OnServiceTelemetryUpdated(object? i_Sender, Vn310Telemetry i_Telemetry)
    {
        UpdateModeAndRaiseIfChanged(i_Telemetry.InsStatus.Mode);

        try
        {
            TelemetryUpdated?.Invoke(this, i_Telemetry);
        }
        catch
        {
            // Service already logs subscriber exceptions on its own event; nothing useful to add here
        }
    }

    // Watchdog fired: no telemetry for 2s. Mirror what OnDisconnectAsync does -- unsubscribe handlers, cancel the connect token, stop the service -- but keep DeviceStatus.Error (instead of going Disconnected) so the UI surfaces WHY we tore down. Without this teardown the service stays IsConnected=true and the next Retry hits StartAsync's "already started" guard
    private void OnServiceStalled(object? i_Sender, EventArgs i_Args)
    {
        m_Service.TelemetryUpdated -= OnServiceTelemetryUpdated;
        m_Service.Stalled -= OnServiceStalled;

        lock (m_CtsLock)
        {
            m_ConnectCts?.Cancel();
            m_ConnectCts?.Dispose();
            m_ConnectCts = null;
        }

        SetStatus(DeviceStatus.Error, "No telemetry for 2s");
        ResetModeAndRaiseIfChanged();

        // Fire-and-forget; StopAsync flips m_IsConnected=false synchronously under its own lock and completes the SDK-level teardown on a thread pool thread, so a subsequent Retry observes a stopped service
        _ = m_Service.StopAsync();
    }
    #endregion
}
