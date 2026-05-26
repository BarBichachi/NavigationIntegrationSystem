using NavigationIntegrationSystem.Core.Logging;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using VectorNav.Math;
using VectorNav.Protocol.Uart;
using VectorNav.Sensor;

namespace NavigationIntegrationSystem.Devices.Implementations.Vn310;

// Per-device-instance service that owns a single VnSensor and decodes incoming packets from both wire formats concurrently (ASCII VNINS and Binary CommonGroup+TimeGroup). Each format is stored as its own per-source latest snapshot; on every packet a merged Vn310Telemetry is composed (shared fields from freshest source; rates/TimeStatus from Binary if fresh; uncertainties from ASCII if fresh) and raised via TelemetryUpdated. Not a singleton; one instance per Vn310InsDevice. Connect is offloaded via Task.Run because the SDK's underlying Connect call is synchronous
public sealed class Vn310TelemetryService : IDisposable
{
    #region Properties
    public bool IsConnected => Volatile.Read(ref m_IsConnected);

    public Vn310Telemetry? LatestTelemetry => Volatile.Read(ref m_LatestTelemetry);

    public string? LastError => Volatile.Read(ref m_LastError);

    // Total packets successfully decoded across both sources. Persists across pane open/close (one service instance lives for the device's whole runtime lifetime)
    public long PacketCount => Interlocked.Read(ref m_AsciiPacketCount) + Interlocked.Read(ref m_BinaryPacketCount);

    public long AsciiPacketCount => Interlocked.Read(ref m_AsciiPacketCount);

    public long BinaryPacketCount => Interlocked.Read(ref m_BinaryPacketCount);

    // Composition of currently-fresh wire sources, derived from the published merged snapshot's freshness flags. Returns Unknown until the first packet arrives
    public Vn310PacketSourceMode LastSourceMode
    {
        get
        {
            Vn310Telemetry? t = Volatile.Read(ref m_LatestTelemetry);
            if (t == null) { return Vn310PacketSourceMode.Unknown; }
            if (t.IsAsciiFresh && t.IsBinaryFresh) { return Vn310PacketSourceMode.Both; }
            if (t.IsAsciiFresh) { return Vn310PacketSourceMode.AsciiOnly; }
            if (t.IsBinaryFresh) { return Vn310PacketSourceMode.BinaryOnly; }
            return Vn310PacketSourceMode.Unknown;
        }
    }

    // Snapshot of recent ASCII-packet timestamps for per-source Hz computation. Returned as a fresh array under the stats lock so the caller can compute rate without racing the writer
    public DateTime[] GetRecentAsciiTimestamps() { lock (m_StatsLock) { return m_RecentAsciiTimestamps.ToArray(); } }

    // Snapshot of recent Binary-packet timestamps for per-source Hz computation
    public DateTime[] GetRecentBinaryTimestamps() { lock (m_StatsLock) { return m_RecentBinaryTimestamps.ToArray(); } }
    #endregion

    #region Private Fields
    // GPS-to-UTC offset in seconds as of 2026. Hardcoded per VN310_PLAN (accepted limitation: will silently drift if a leap second is added)
    private const int c_GpsToUtcLeapOffsetSec = -18;

    // Watchdog window: declare Stalled if no packet of EITHER source arrives for this duration. Device-level liveness check; per-source staleness is separate (c_SourceStalenessMs)
    private const int c_WatchdogMs = 2000;

    // Per-source staleness threshold. A source's exclusive fields (rates+TimeStatus for Binary; uncertainties for ASCII) are treated as live only if a packet from that source arrived within this window. Set to match the main watchdog so single-source-dead detection happens at the same scale
    private const int c_SourceStalenessMs = 2000;

    // Rate limit for "incompatible binary packet" log noise (the sensor may emit binary groups other than what we configured for)
    private const int c_IncompatibleLogIntervalSec = 60;

    // Size cap for the per-source recent-timestamps rings used by Hz computation. 256 covers even the highest plausible VN310 rate (~400Hz) over a 1s window with headroom; anything older than 1s gets trimmed in the writer
    private const int c_RecentTimestampsRingCap = 256;

    // Initial-handshake timeout: how long StartAsync waits for the first packet (of any source) AFTER SDK Connect succeeds before failing. Without this guard, SDK reports "Connected" on a port that's wired up but has no sensor on the other end -- we'd transition to Connected, the watchdog would fire 2s later, and auto-reconnect would loop forever. By failing StartAsync we surface this as Connecting->Error (not Connected->Error), short-circuiting the auto-reconnect path
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

    // Per-source latest parsed snapshots. Only OnAsyncPacketReceived writes these (SDK packet thread, single writer), so no lock is required between reader and writer; readers Volatile.Read for a coherent snapshot. The IsAsciiFresh/IsBinaryFresh flags on these per-source objects are unused -- freshness is evaluated against PacketReceivedAt at merge time
    private Vn310Telemetry? m_LatestAscii;
    private Vn310Telemetry? m_LatestBinary;

    // Per-source counters and timestamp rings. Counters use Interlocked; rings are guarded by m_StatsLock because they're read on the inspect VM's UI-thread timer tick and written on the SDK packet thread
    private long m_AsciiPacketCount;
    private long m_BinaryPacketCount;
    private readonly Queue<DateTime> m_RecentAsciiTimestamps = new(c_RecentTimestampsRingCap);
    private readonly Queue<DateTime> m_RecentBinaryTimestamps = new(c_RecentTimestampsRingCap);
    private readonly object m_StatsLock = new();

    // Last source-mode value emitted to the log. Used to fire transition messages (e.g. Both->BinaryOnly when ASCII stalls) at most once per transition rather than per-packet. Touched only from the SDK packet thread inside OnAsyncPacketReceived
    private Vn310PacketSourceMode m_LastLoggedMode = Vn310PacketSourceMode.Unknown;

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

                // Proof-of-life received: commit to Connected state, arm the runtime watchdog. The first packet has already been processed by OnAsyncPacketReceived (m_LatestTelemetry / per-source state are populated) -- the caller will see live data the moment Connected fires
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

    // Parses an ASCII VNINS packet into a per-source snapshot. Yaw is wrapped to [0, 360). Rates / TimeStatus default to zero/empty because ASCII VNINS doesn't carry them -- the merge layer is responsible for sourcing those from the latest fresh Binary snapshot
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
            // Freshness flags on per-source snapshots are unused (the merge function recomputes them); set neutrally so accidental direct consumption isn't misleading
            IsAsciiFresh = false,
            IsBinaryFresh = false,
            PacketReceivedAt = i_ReceivedAt
        };
    }

    // Parses a binary packet with the expected CommonGroup + TimeGroup layout into a per-source snapshot. Extraction order is locked by the VN310 ICD and must match the order the sensor serializes the fields. Yaw is wrapped to [0, 360). Uncertainties default to 0f because our subscribed binary groups don't include them -- the merge layer sources those from the latest fresh ASCII snapshot. Returns null if the binary group layout doesn't match (e.g. sensor was reconfigured for a different binary subscription via Control Center)
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
            IsAsciiFresh = false,
            IsBinaryFresh = false,
            PacketReceivedAt = i_ReceivedAt
        };
    }

    // Composes the published Vn310Telemetry from per-source snapshots and freshness. Called from OnAsyncPacketReceived on the SDK packet thread (single writer of m_LatestAscii / m_LatestBinary). Reads of those fields here are race-free; readers of the published m_LatestTelemetry see whatever was last Volatile.Written
    private Vn310Telemetry BuildMerged(DateTime i_NowUtc)
    {
        Vn310Telemetry? a = m_LatestAscii;
        Vn310Telemetry? b = m_LatestBinary;

        bool asciiFresh = a != null && (i_NowUtc - a.PacketReceivedAt).TotalMilliseconds < c_SourceStalenessMs;
        bool binaryFresh = b != null && (i_NowUtc - b.PacketReceivedAt).TotalMilliseconds < c_SourceStalenessMs;

        // Shared-field source preference: freshest of the two; fall back to whichever exists if exactly one is non-null. BuildMerged is only called after at least one parse succeeded this tick, so at least one of (a, b) is non-null
        Vn310Telemetry shared = ChooseSharedSource(a, b, asciiFresh, binaryFresh);

        // UtcTime preference: prefer Binary if fresh (real Y/M/D from sensor, validated by TimeStatus). Fall back to ASCII (has the midnight-wrap caveat: it reconstructs the date from i_ReceivedAt.Date which can roll a second too late). Last resort: whatever shared has
        DateTime utc = binaryFresh ? b!.UtcTime : (asciiFresh ? a!.UtcTime : shared.UtcTime);

        return new Vn310Telemetry
        {
            UtcTime = utc,
            LatDeg = shared.LatDeg,
            LonDeg = shared.LonDeg,
            AltM = shared.AltM,
            YawDeg = shared.YawDeg,
            PitchDeg = shared.PitchDeg,
            RollDeg = shared.RollDeg,
            VelNorth = shared.VelNorth,
            VelEast = shared.VelEast,
            VelDown = shared.VelDown,
            Speed = shared.Speed,
            InsStatus = shared.InsStatus,

            // Binary-exclusive (rates + TimeStatus)
            YawRateDegS = binaryFresh ? b!.YawRateDegS : 0.0,
            PitchRateDegS = binaryFresh ? b!.PitchRateDegS : 0.0,
            RollRateDegS = binaryFresh ? b!.RollRateDegS : 0.0,
            TimeStatus = binaryFresh ? b!.TimeStatus : new Vn310TimeStatus(),

            // ASCII-exclusive (uncertainties)
            AttUncertainty = asciiFresh ? a!.AttUncertainty : 0f,
            PosUncertainty = asciiFresh ? a!.PosUncertainty : 0f,
            VelUncertainty = asciiFresh ? a!.VelUncertainty : 0f,

            IsAsciiFresh = asciiFresh,
            IsBinaryFresh = binaryFresh,
            PacketReceivedAt = i_NowUtc
        };
    }

    // Selects the source whose values populate shared fields (YPR, LLA, NED, InsStatus). Freshest wins; ties prefer ASCII (rare, since 100Hz Binary almost always wins on a typical dual-stream config); falls back through staleness if only one source has ever been seen
    private static Vn310Telemetry ChooseSharedSource(Vn310Telemetry? i_Ascii, Vn310Telemetry? i_Binary, bool i_AsciiFresh, bool i_BinaryFresh)
    {
        if (i_AsciiFresh && i_BinaryFresh) { return i_Ascii!.PacketReceivedAt >= i_Binary!.PacketReceivedAt ? i_Ascii : i_Binary; }
        if (i_AsciiFresh) { return i_Ascii!; }
        if (i_BinaryFresh) { return i_Binary!; }
        // Neither fresh on the merge tick is only possible if the current packet hasn't been folded in yet, which BuildMerged's caller arranges to be false. Defensive fallback to the newer of any non-null sources
        if (i_Ascii != null && (i_Binary == null || i_Ascii.PacketReceivedAt >= i_Binary.PacketReceivedAt)) { return i_Ascii; }
        return i_Binary!;
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

    // Emits a log line only when the source-mode composition changes. Fires from the SDK packet thread, called per-packet. Transitions: Unknown->X on first packet; X->Both when a previously-silent source starts; Both->X when one source stalls past c_SourceStalenessMs while the other keeps flowing
    private void LogModeTransitionIfChanged(Vn310PacketSourceMode i_Current)
    {
        if (i_Current == m_LastLoggedMode) { return; }
        Vn310PacketSourceMode previous = m_LastLoggedMode;
        m_LastLoggedMode = i_Current;

        string msg = (previous, i_Current) switch
        {
            (Vn310PacketSourceMode.Unknown, Vn310PacketSourceMode.Both) => "VN310 dual stream established (ASCII + Binary) -- all fields live",
            (Vn310PacketSourceMode.Unknown, Vn310PacketSourceMode.AsciiOnly) => "VN310 ASCII-only stream established -- rates and TimeStatus unavailable",
            (Vn310PacketSourceMode.Unknown, Vn310PacketSourceMode.BinaryOnly) => "VN310 Binary-only stream established -- uncertainties unavailable",
            (Vn310PacketSourceMode.Both, Vn310PacketSourceMode.BinaryOnly) => "VN310 ASCII stream stale (>2s); uncertainties unavailable",
            (Vn310PacketSourceMode.Both, Vn310PacketSourceMode.AsciiOnly) => "VN310 Binary stream stale (>2s); rates and TimeStatus unavailable",
            (Vn310PacketSourceMode.BinaryOnly, Vn310PacketSourceMode.Both) => "VN310 ASCII stream recovered -- all fields live",
            (Vn310PacketSourceMode.AsciiOnly, Vn310PacketSourceMode.Both) => "VN310 Binary stream recovered -- all fields live",
            _ => $"VN310 source mode changed: {previous} -> {i_Current}"
        };
        m_LogService.Info(nameof(Vn310TelemetryService), msg);
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
    // Fires on the SDK's internal read thread when a packet arrives. Decodes, stores into the matching per-source slot, builds a merged snapshot, updates per-source stats, resets the watchdog, then raises TelemetryUpdated. All writes to per-source state happen on this single thread; readers Volatile.Read m_LatestTelemetry for the published merge
    private void OnAsyncPacketReceived(object? i_Sender, PacketFoundEventArgs i_Args)
    {
        Packet packet = i_Args.FoundPacket;
        DateTime receivedAt = DateTime.UtcNow;

        if (packet.IsError)
        {
            m_LogService.Warn(nameof(Vn310TelemetryService), $"Sensor reported packet error: {packet.Error}");
            return;
        }

        // Route by wire type. Each branch parses, stores into its per-source slot, and updates per-source stats. ASCII non-VNINS / non-async types and unrecognized binary layouts are dropped before reaching the merge
        bool routed = false;
        try
        {
            if (packet.Type == PacketType.Ascii)
            {
                if (!packet.IsAsciiAsync || packet.AsciiAsyncType != AsciiAsync.VNINS) { return; }
                Vn310Telemetry asciiSnap = ParseAscii(packet, receivedAt);
                m_LatestAscii = asciiSnap;
                Interlocked.Increment(ref m_AsciiPacketCount);
                PushTimestamp(m_RecentAsciiTimestamps, receivedAt);
                routed = true;
            }
            else if (packet.Type == PacketType.Binary)
            {
                Vn310Telemetry? binSnap = ParseBinary(packet, receivedAt);
                if (binSnap == null) { return; } // incompatible layout; already logged (rate-limited)
                m_LatestBinary = binSnap;
                Interlocked.Increment(ref m_BinaryPacketCount);
                PushTimestamp(m_RecentBinaryTimestamps, receivedAt);
                routed = true;
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

        if (!routed) { return; }

        // Build and publish the merged snapshot. Single writer (this thread) -> simple Volatile.Write is sufficient for cross-thread visibility
        Vn310Telemetry merged = BuildMerged(receivedAt);
        Volatile.Write(ref m_LatestTelemetry, merged);

        // Signal the initial-handshake gate if StartAsync is waiting for proof-of-life. TrySetResult is no-op on subsequent packets (already completed) and idempotent if the timeout already fired (already cancelled)
        Volatile.Read(ref m_FirstPacketTcs)?.TrySetResult(true);

        // Source-mode transition logging derives the current mode directly from the merge result's freshness flags, so it stays in sync with what consumers see
        Vn310PacketSourceMode currentMode = (merged.IsAsciiFresh, merged.IsBinaryFresh) switch
        {
            (true, true) => Vn310PacketSourceMode.Both,
            (true, false) => Vn310PacketSourceMode.AsciiOnly,
            (false, true) => Vn310PacketSourceMode.BinaryOnly,
            _ => Vn310PacketSourceMode.Unknown
        };
        LogModeTransitionIfChanged(currentMode);

        // Reset the watchdog window. Grab the timer ref under lock to avoid racing with StopAsync nulling it out. ANY packet of either source resets it -- the watchdog detects total device silence, not per-source silence
        Timer? watchdog;
        lock (m_StateLock) { watchdog = m_Watchdog; }
        watchdog?.Change(c_WatchdogMs, Timeout.Infinite);

        try { TelemetryUpdated?.Invoke(this, merged); }
        catch (Exception ex)
        {
            // Don't let a buggy subscriber take down the SDK's read thread
            m_LogService.Error(nameof(Vn310TelemetryService), $"TelemetryUpdated subscriber threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Pushes a timestamp into a per-source ring under the stats lock, trimming entries older than 1s (rate-window) and enforcing the hard ring cap. Hot path; keep allocations zero
    private void PushTimestamp(Queue<DateTime> i_Ring, DateTime i_Timestamp)
    {
        lock (m_StatsLock)
        {
            DateTime cutoff = i_Timestamp - TimeSpan.FromSeconds(1);
            while (i_Ring.Count > 0 && i_Ring.Peek() < cutoff)
            {
                i_Ring.Dequeue();
            }
            while (i_Ring.Count >= c_RecentTimestampsRingCap)
            {
                i_Ring.Dequeue();
            }
            i_Ring.Enqueue(i_Timestamp);
        }
    }

    // Fires on the timer thread when no packet of either source has arrived within c_WatchdogMs. Sets LastError and raises Stalled. Does not auto-disconnect; the owning device decides how to react
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
