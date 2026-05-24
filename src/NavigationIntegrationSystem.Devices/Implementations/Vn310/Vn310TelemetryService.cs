using NavigationIntegrationSystem.Core.Logging;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using VectorNav.Math;
using VectorNav.Protocol.Uart;
using VectorNav.Sensor;

namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// Per-device-instance service that owns a single VnSensor, decodes incoming packets (ASCII VNINS or binary with the expected group layout), and raises TelemetryUpdated for downstream consumers. Not a singleton; one instance per Vn310InsDevice. Connect is offloaded via Task.Run because the SDK's underlying Connect call is synchronous.
public sealed class Vn310TelemetryService : IDisposable
{
    #region Properties
    public bool IsConnected => Volatile.Read(ref m_IsConnected);

    public Vn310Telemetry? LatestTelemetry => Volatile.Read(ref m_LatestTelemetry);

    public string? LastError => Volatile.Read(ref m_LastError);

    // Total packets successfully decoded since this service was constructed. Persists across pane open/close so the inspect view's counter is meaningful. Reset only when the service is recreated (one per device instance, which lives for the device's whole runtime lifetime)
    public long PacketCount => Interlocked.Read(ref m_PacketCount);

    // The wire format of the most recently decoded packet (or Unknown if none yet). Inspect view uses this to render the ASCII-vs-Binary banners
    public Vn310PacketSourceMode LastSourceMode { get { lock (m_StatsLock) { return m_LastSourceMode; } } }

    // Snapshot of recent packet-arrival timestamps for Hz computation. Returned as a fresh array under the stats lock so the caller can compute rate without racing the writer
    public DateTime[] GetRecentPacketTimestamps() { lock (m_StatsLock) { return m_RecentPacketTimestamps.ToArray(); } }
    #endregion

    #region Private Fields
    // GPS-to-UTC offset in seconds as of 2026. Hardcoded per VN310_PLAN (accepted limitation: will silently drift if a leap second is added)
    private const int c_GpsToUtcLeapOffsetSec = -18;

    // Watchdog window: declare Stalled if no packet arrives for this duration
    private const int c_WatchdogMs = 2000;

    // Rate limit for "incompatible binary packet" log noise (the sensor may emit groups other than what we configured for)
    private const int c_IncompatibleLogIntervalSec = 60;

    // Size cap for the recent-timestamps ring used by Hz computation. 256 is more than enough to cover the highest plausible VN310 rate (~400Hz) over a 1s window with headroom; anything older than 1s gets trimmed in the writer
    private const int c_RecentTimestampsRingCap = 256;

    // Initial-handshake timeout: how long StartAsync waits for the first packet AFTER SDK Connect succeeds before failing. Without this guard, SDK reports "Connected" on a port that's wired up but has no sensor on the other end -- we'd transition to Connected, the watchdog would fire 2s later, and auto-reconnect would loop forever. By failing the StartAsync we surface this as Connecting->Error (not Connected->Error), which short-circuits the auto-reconnect path. 3s is generous enough to cover sensor warm-up + SDK first-packet jitter
    private const int c_InitialPacketTimeoutMs = 3000;

    private static readonly CommonGroup s_ExpectedCommonGroup =
        CommonGroup.YawPitchRoll | CommonGroup.AngularRate | CommonGroup.Position | CommonGroup.Velocity | CommonGroup.InsStatus;

    private static readonly TimeGroup s_ExpectedTimeGroup = TimeGroup.TimeUtc | TimeGroup.TimeStatus;

    private readonly ILogService m_LogService;
    private readonly object m_StateLock = new();

    private VnSensor? m_Sensor;
    private Timer? m_Watchdog;
    private Vn310Telemetry? m_LatestTelemetry;
    private string? m_LastError;
    private bool m_IsConnected;
    private bool m_Disposed;
    private DateTime m_LastIncompatibleLogUtc = DateTime.MinValue;

    // Packet stats. Read on the inspect VM's UI-thread timer tick; written on the SDK packet thread. Lock-guarded for the source-mode + ring (PacketCount uses Interlocked)
    private long m_PacketCount;
    private Vn310PacketSourceMode m_LastSourceMode = Vn310PacketSourceMode.Unknown;
    private readonly Queue<DateTime> m_RecentPacketTimestamps = new Queue<DateTime>(c_RecentTimestampsRingCap);
    private readonly object m_StatsLock = new object();

    // First-packet TCS used by StartAsync to wait for proof-of-life from the sensor. Non-null only during the initial-handshake window (between SDK Connect succeeding and first packet arriving). OnAsyncPacketReceived signals it on first packet so StartAsync returns; if timeout fires before first packet, StartAsync throws TimeoutException and the caller treats Connecting->Error (no auto-reconnect)
    private TaskCompletionSource<bool>? m_FirstPacketTcs;
    #endregion

    #region Events
    public event EventHandler<Vn310Telemetry>? TelemetryUpdated;
    public event EventHandler? Stalled;
    #endregion

    #region Constructors
    public Vn310TelemetryService(ILogService i_LogService)
    {
        m_LogService = i_LogService;
    }
    #endregion

    #region Functions
    // Opens the serial port, attaches the packet listener, AWAITS the first telemetry packet (initial handshake), then arms the watchdog and returns. Throws on Connect failure OR if no packet arrives within c_InitialPacketTimeoutMs (caller maps to DeviceStatus.Error). Idempotent: calling while already connected throws InvalidOperationException
    public Task StartAsync(string i_ComPort, int i_BaudRate, CancellationToken i_CancellationToken)
    {
        ThrowIfDisposed();

        lock (m_StateLock)
        {
            if (m_IsConnected)
            {
                throw new InvalidOperationException("VN310 telemetry service is already started.");
            }
        }

        // vs.Connect is synchronous and blocking; offload so we don't block the caller's thread
        return Task.Run(async () =>
        {
            i_CancellationToken.ThrowIfCancellationRequested();

            VnSensor sensor = new VnSensor();
            TaskCompletionSource<bool> firstPacketTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool handlerAttached = false;
            try
            {
                sensor.Connect(i_ComPort, (uint)i_BaudRate);
                sensor.ResponseTimeoutMs = 3000;
                Thread.Sleep(1000); // SDK reference uses ~1s settle delay after Connect to let the port stabilize before subscribing

                if (!sensor.IsConnected)
                {
                    throw new InvalidOperationException($"VN310 Connect on {i_ComPort} @ {i_BaudRate} returned IsConnected=false.");
                }

                // Wire up the first-packet gate BEFORE subscribing -- OnAsyncPacketReceived checks m_FirstPacketTcs and signals it on first packet. Must be set before the handler attaches or we could miss the first packet (race window)
                Volatile.Write(ref m_FirstPacketTcs, firstPacketTcs);
                sensor.AsyncPacketReceived += OnAsyncPacketReceived;
                handlerAttached = true;

                // Wait for first packet OR initial-handshake timeout OR external cancel. SDK Connect "succeeding" on a wired-but-dead link is the exact reason this exists -- we need proof-of-life before reporting Connected, otherwise auto-reconnect would flap forever on a misconfigured sensor
                using (CancellationTokenSource initialCts = CancellationTokenSource.CreateLinkedTokenSource(i_CancellationToken))
                {
                    initialCts.CancelAfter(c_InitialPacketTimeoutMs);
                    using (initialCts.Token.Register(() => firstPacketTcs.TrySetCanceled()))
                    {
                        try
                        {
                            await firstPacketTcs.Task.ConfigureAwait(false);
                        }
                        catch (TaskCanceledException)
                        {
                            if (i_CancellationToken.IsCancellationRequested) { throw new OperationCanceledException(i_CancellationToken); }
                            throw new TimeoutException($"VN310 connected on {i_ComPort} but no telemetry received within {c_InitialPacketTimeoutMs / 1000}s. Check that the sensor is powered on and configured to stream.");
                        }
                    }
                }

                // Proof-of-life received: commit to Connected state, arm the runtime watchdog. The first packet has already been processed by OnAsyncPacketReceived (m_LatestTelemetry / PacketCount / etc. are populated) -- the caller will see live data the moment Connected fires
                lock (m_StateLock)
                {
                    m_Sensor = sensor;
                    Volatile.Write(ref m_LastError, null);
                    Volatile.Write(ref m_IsConnected, true);
                    m_Watchdog = new Timer(OnWatchdogTick, null, c_WatchdogMs, Timeout.Infinite);
                }
                Volatile.Write(ref m_FirstPacketTcs, null);

                m_LogService.Info(nameof(Vn310TelemetryService), $"VN310 connected on {i_ComPort} @ {i_BaudRate} (first packet received)");
            }
            catch (Exception ex)
            {
                Volatile.Write(ref m_LastError, ex.Message);
                Volatile.Write(ref m_FirstPacketTcs, null);
                // Best-effort cleanup of the half-initialized sensor before rethrowing. Unsubscribe first to avoid handler firing during/after Disconnect
                if (handlerAttached) { try { sensor.AsyncPacketReceived -= OnAsyncPacketReceived; } catch { /* swallow */ } }
                try { sensor.Disconnect(); } catch { /* swallow; already in failure path */ }
                throw;
            }
        }, i_CancellationToken);
    }

    // Detaches the listener, stops the watchdog, and closes the serial port. Safe to call when not connected (no-op)
    public Task StopAsync()
    {
        ThrowIfDisposed();

        VnSensor? sensorToClose;
        Timer? watchdogToDispose;

        lock (m_StateLock)
        {
            if (!m_IsConnected)
            {
                return Task.CompletedTask;
            }

            sensorToClose = m_Sensor;
            watchdogToDispose = m_Watchdog;
            m_Sensor = null;
            m_Watchdog = null;
            Volatile.Write(ref m_IsConnected, false);
        }

        // Run outside the lock so SDK teardown (which may join its read thread) can't deadlock against the packet callback
        return Task.Run(() =>
        {
            try
            {
                watchdogToDispose?.Dispose();

                if (sensorToClose != null)
                {
                    sensorToClose.AsyncPacketReceived -= OnAsyncPacketReceived;
                    sensorToClose.Disconnect();
                }

                m_LogService.Info(nameof(Vn310TelemetryService), "VN310 disconnected");
            }
            catch (Exception ex)
            {
                m_LogService.Error(nameof(Vn310TelemetryService), $"Error during VN310 disconnect: {ex.Message}");
            }
        });
    }

    // Parses an ASCII VNINS packet. Yaw is wrapped to [0, 360). Rates default to 0 (ASCII VNINS does not include them). UTC is reconstructed from today's UTC date + the time-of-day field minus the GPS leap offset
    private Vn310Telemetry ParseAscii(Packet i_Packet, DateTime i_ReceivedAt)
    {
        double time;
        ushort week;
        ushort status;
        vec3f ypr;
        vec3d lla;
        vec3f nedVel;
        float attUncertainty;
        float posUncertainty;
        float velUncertainty;

        i_Packet.ParseVNINS(out time, out week, out status, out ypr, out lla, out nedVel, out attUncertainty, out posUncertainty, out velUncertainty);

        TimeSpan timeOfDay = TimeSpan.FromSeconds((time + c_GpsToUtcLeapOffsetSec) % 86400);
        DateTime utc = i_ReceivedAt.Date + timeOfDay;

        Vn310InsStatus insStatus = new Vn310InsStatus { RawData = status };

        return new Vn310Telemetry
        {
            UtcTime = utc,
            LatDeg = lla.X,
            LonDeg = lla.Y,
            AltM = lla.Z,
            YawDeg = WrapYawTo360(ypr.X),
            PitchDeg = ypr.Y,
            RollDeg = ypr.Z,
            YawRateDegS = 0.0,
            PitchRateDegS = 0.0,
            RollRateDegS = 0.0,
            VelNorth = nedVel.X,
            VelEast = nedVel.Y,
            VelDown = nedVel.Z,
            Speed = ComputeSpeed(nedVel.X, nedVel.Y, nedVel.Z),
            AttUncertainty = attUncertainty,
            PosUncertainty = posUncertainty,
            VelUncertainty = velUncertainty,
            InsStatus = insStatus,
            TimeStatus = new Vn310TimeStatus(),
            HasRates = false,
            PacketReceivedAt = i_ReceivedAt
        };
    }

    // Parses a binary packet with the expected CommonGroup + TimeGroup layout. Extraction order is locked by the VN310 ICD and must match the order the sensor serializes the fields. Yaw is wrapped to [0, 360)
    private Vn310Telemetry? ParseBinary(Packet i_Packet, DateTime i_ReceivedAt)
    {
        if (!i_Packet.IsCompatible(s_ExpectedCommonGroup, s_ExpectedTimeGroup, ImuGroup.None, GpsGroup.None, AttitudeGroup.None, InsGroup.None))
        {
            LogIncompatibleBinaryPacketRateLimited();
            return null;
        }

        vec3f ypr = i_Packet.ExtractVec3f();
        vec3f rates = i_Packet.ExtractVec3f();
        vec3d lla = i_Packet.ExtractVec3d();
        vec3f nedVel = i_Packet.ExtractVec3f();
        ushort statusRaw = i_Packet.ExtractUint16();

        byte rawYear = i_Packet.ExtractUint8();
        byte rawMonth = i_Packet.ExtractUint8();
        byte rawDay = i_Packet.ExtractUint8();
        byte rawHour = i_Packet.ExtractUint8();
        byte rawMinute = i_Packet.ExtractUint8();
        byte rawSecond = i_Packet.ExtractUint8();
        ushort rawMillisecond = i_Packet.ExtractUint16();
        byte rawTimeStatus = i_Packet.ExtractUint8();

        DateTime utc = SafeBuildUtc(rawYear, rawMonth, rawDay, rawHour, rawMinute, rawSecond, rawMillisecond, i_ReceivedAt);

        return new Vn310Telemetry
        {
            UtcTime = utc,
            LatDeg = lla.X,
            LonDeg = lla.Y,
            AltM = lla.Z,
            YawDeg = WrapYawTo360(ypr.X),
            PitchDeg = ypr.Y,
            RollDeg = ypr.Z,
            YawRateDegS = rates.X,
            PitchRateDegS = rates.Y,
            RollRateDegS = rates.Z,
            VelNorth = nedVel.X,
            VelEast = nedVel.Y,
            VelDown = nedVel.Z,
            Speed = ComputeSpeed(nedVel.X, nedVel.Y, nedVel.Z),
            AttUncertainty = 0f,
            PosUncertainty = 0f,
            VelUncertainty = 0f,
            InsStatus = new Vn310InsStatus { RawData = statusRaw },
            TimeStatus = new Vn310TimeStatus { RawData = rawTimeStatus },
            HasRates = true,
            PacketReceivedAt = i_ReceivedAt
        };
    }

    // Builds a DateTime from raw Y/M/D/H/M/S/ms bytes. Falls back to the packet-received time if the bytes are not yet calendar-valid (sensor warm-up before TimeStatus.IsValid becomes true)
    private static DateTime SafeBuildUtc(byte i_Year, byte i_Month, byte i_Day, byte i_Hour, byte i_Minute, byte i_Second, ushort i_Millisecond, DateTime i_Fallback)
    {
        try
        {
            int fullYear = (i_Year < 100) ? (2000 + i_Year) : i_Year;
            return new DateTime(fullYear, i_Month, i_Day, i_Hour, i_Minute, i_Second, i_Millisecond, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return i_Fallback;
        }
    }

    // Wraps yaw from VN's -180..+180 range to NIS's 0..360 display convention
    private static double WrapYawTo360(double i_YawDeg)
    {
        double wrapped = (i_YawDeg + 360.0) % 360.0;
        return wrapped < 0 ? wrapped + 360.0 : wrapped;
    }

    // Magnitude of the NED velocity vector
    private static double ComputeSpeed(double i_North, double i_East, double i_Down)
    {
        return System.Math.Sqrt(i_North * i_North + i_East * i_East + i_Down * i_Down);
    }

    // Rate-limits "incompatible binary packet" warnings so a misconfigured sensor doesn't spam the log
    private void LogIncompatibleBinaryPacketRateLimited()
    {
        DateTime now = DateTime.UtcNow;
        if ((now - m_LastIncompatibleLogUtc).TotalSeconds < c_IncompatibleLogIntervalSec)
        {
            return;
        }
        m_LastIncompatibleLogUtc = now;
        m_LogService.Warn(nameof(Vn310TelemetryService), "Received binary packet that does not match expected group layout; skipping. Check sensor factory configuration.");
    }

    private void ThrowIfDisposed()
    {
        if (m_Disposed)
        {
            throw new ObjectDisposedException(nameof(Vn310TelemetryService));
        }
    }

    // Bounded sync-wait on Dispose so the OS-level serial port handle is actually released before the process exits. The previous fire-and-forget StopAsyncSafe() returned immediately and the port could remain held by the SDK's read thread for several seconds after process exit (visible as "port busy" if the user immediately relaunches). Cap the wait at 2s so a hung SDK can't block shutdown forever. Disconnect via InsDevicesShutdownService should run first in normal shutdown -- this is a backstop
    private const int c_DisposeTimeoutMs = 2000;

    public void Dispose()
    {
        if (m_Disposed)
        {
            return;
        }
        m_Disposed = true;
        try
        {
            Task stopTask = StopAsyncSafe();
            stopTask.Wait(c_DisposeTimeoutMs);
        }
        catch
        {
            // Dispose must not throw. StopAsyncSafe already swallows its own errors; this catches anything Task.Wait wraps in AggregateException
        }
    }

    // StopAsync wrapper that swallows ObjectDisposedException so Dispose can call it without checking state
    private async Task StopAsyncSafe()
    {
        try
        {
            // Inline the StopAsync body here so Dispose path doesn't trip the ThrowIfDisposed guard
            VnSensor? sensorToClose;
            Timer? watchdogToDispose;
            lock (m_StateLock)
            {
                if (!m_IsConnected) { return; }
                sensorToClose = m_Sensor;
                watchdogToDispose = m_Watchdog;
                m_Sensor = null;
                m_Watchdog = null;
                Volatile.Write(ref m_IsConnected, false);
            }
            await Task.Run(() =>
            {
                try
                {
                    watchdogToDispose?.Dispose();
                    if (sensorToClose != null)
                    {
                        sensorToClose.AsyncPacketReceived -= OnAsyncPacketReceived;
                        sensorToClose.Disconnect();
                    }
                }
                catch { /* swallow during dispose */ }
            }).ConfigureAwait(false);
        }
        catch { /* swallow during dispose */ }
    }
    #endregion

    #region Event Handlers
    // Fires on the SDK's internal read thread when a packet arrives. Decodes, updates LatestTelemetry, resets the watchdog, then raises TelemetryUpdated
    private void OnAsyncPacketReceived(object? i_Sender, PacketFoundEventArgs i_Args)
    {
        Packet packet = i_Args.FoundPacket;
        DateTime receivedAt = DateTime.UtcNow;

        if (packet.IsError)
        {
            m_LogService.Warn(nameof(Vn310TelemetryService), $"Sensor reported packet error: {packet.Error}");
            return;
        }

        Vn310Telemetry? telemetry = null;
        try
        {
            if (packet.Type == PacketType.Ascii)
            {
                // Ignore NMEA passthrough and any non-VNINS VectorNav ASCII async types
                if (!packet.IsAsciiAsync || packet.AsciiAsyncType != AsciiAsync.VNINS)
                {
                    return;
                }
                telemetry = ParseAscii(packet, receivedAt);
            }
            else if (packet.Type == PacketType.Binary)
            {
                telemetry = ParseBinary(packet, receivedAt);
                if (telemetry == null) { return; } // already logged (rate-limited)
            }
            else
            {
                return;
            }
        }
        catch (Exception ex)
        {
            m_LogService.Error(nameof(Vn310TelemetryService), $"Failed to parse incoming packet: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        Volatile.Write(ref m_LatestTelemetry, telemetry);

        // Signal the initial-handshake gate if StartAsync is waiting for proof-of-life. TrySetResult is no-op on subsequent packets (already completed), and idempotent if the timeout already fired (already cancelled). Done BEFORE stats / event raise so StartAsync's await returns as fast as possible
        Volatile.Read(ref m_FirstPacketTcs)?.TrySetResult(true);

        // Update packet stats: increment count, refresh source mode, push timestamp into the rolling ring (trim entries older than 1s and enforce hard cap to bound memory)
        Interlocked.Increment(ref m_PacketCount);
        lock (m_StatsLock)
        {
            m_LastSourceMode = packet.Type == PacketType.Binary ? Vn310PacketSourceMode.Binary : Vn310PacketSourceMode.Ascii;
            DateTime cutoff = receivedAt - TimeSpan.FromSeconds(1);
            while (m_RecentPacketTimestamps.Count > 0 && m_RecentPacketTimestamps.Peek() < cutoff)
            {
                m_RecentPacketTimestamps.Dequeue();
            }
            while (m_RecentPacketTimestamps.Count >= c_RecentTimestampsRingCap)
            {
                m_RecentPacketTimestamps.Dequeue();
            }
            m_RecentPacketTimestamps.Enqueue(receivedAt);
        }

        // Reset the watchdog window. Grab the timer ref under lock to avoid racing with StopAsync nulling it out
        Timer? watchdog;
        lock (m_StateLock) { watchdog = m_Watchdog; }
        watchdog?.Change(c_WatchdogMs, Timeout.Infinite);

        try { TelemetryUpdated?.Invoke(this, telemetry); }
        catch (Exception ex)
        {
            // Don't let a buggy subscriber take down the SDK's read thread
            m_LogService.Error(nameof(Vn310TelemetryService), $"TelemetryUpdated subscriber threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Fires on the timer thread when no packet has arrived within c_WatchdogMs. Sets LastError and raises Stalled. Does not auto-disconnect; the owning device decides how to react
    private void OnWatchdogTick(object? i_State)
    {
        if (!Volatile.Read(ref m_IsConnected)) { return; }

        Volatile.Write(ref m_LastError, $"No telemetry for {c_WatchdogMs / 1000}s");
        m_LogService.Warn(nameof(Vn310TelemetryService), Volatile.Read(ref m_LastError) ?? "stalled");

        try { Stalled?.Invoke(this, EventArgs.Empty); }
        catch (Exception ex)
        {
            m_LogService.Error(nameof(Vn310TelemetryService), $"Stalled subscriber threw: {ex.GetType().Name}: {ex.Message}");
        }
    }
    #endregion
}
