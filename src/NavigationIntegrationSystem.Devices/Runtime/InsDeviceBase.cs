using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models.Devices;
using NavigationIntegrationSystem.Devices.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.Devices.Runtime;

// Implements common runtime behavior for INS devices
public abstract class InsDeviceBase : IInsDevice
{
    #region Private Fields
    private DeviceStatus m_Status;
    private string? m_LastError;
    private CancellationTokenSource? m_ConnectCts;
    private readonly object m_CtsLock = new object();
    private readonly ILogService m_LogService;

    // Auto-reconnect (Config.AutoReconnect=true): runs when an active device unexpectedly drops to Error (watchdog stall, USB unplug, etc.). Separate token from m_ConnectCts so user-initiated Disconnect/Retry doesn't tear down an in-flight backoff loop's bookkeeping, and so the loop can be cancelled without affecting the next user-initiated connect
    private CancellationTokenSource? m_AutoReconnectCts;
    private bool m_IsAutoReconnecting;
    private string? m_ReconnectStatusText;
    private readonly object m_AutoReconnectLock = new object();
    // Class-level attempt counter (NOT loop-local). Survives across multiple loop spawns triggered by Connected->Error flaps -- otherwise each flap resets backoff to 5s and the schedule never progresses. Reset to 0 only when a Connect is stable for c_StableConnectedResetMs (the connection actually held long enough to count as "recovered")
    private int m_AutoReconnectAttempt;
    private DateTime? m_LastConnectedAtUtc;
    // After this long Connected without an Error, treat the next drop as a fresh problem and reset the backoff counter. Anything less = the previous "Connected" was probably a flap (e.g. SDK reported connect, but link was actually still bad) and the backoff should keep progressing
    private const int c_StableConnectedResetMs = 30000;
    // Backoff schedule (seconds): 5s gives the user time to perceive the wait and click Cancel before the next attempt fires. Caps at 60s after enough failures. Total: 5, 10, 30, 60, then 60 repeating until reconnected / user clicks Disconnect / AutoReconnect turned off
    private static readonly int[] s_AutoReconnectBackoffSec = { 5, 10, 30, 60 };
    // Ticks the "Reconnecting in Ns..." countdown text at 1Hz so the UI can show a live countdown rather than a frozen "Reconnecting in 5s..." that never updates
    private const int c_CountdownTickMs = 1000;
    #endregion

    #region Properties
    public DeviceDefinition Definition { get; }
    public DeviceStatus Status => m_Status;
    public string? LastError => m_LastError;
    public DeviceConfig Config { get; }
    public virtual DeviceModeSnapshot? CurrentMode => null;
    public bool IsAutoReconnecting { get { lock (m_AutoReconnectLock) { return m_IsAutoReconnecting; } } }
    // Updated by the backoff loop ("Reconnecting in 5s...", "Reconnecting...", null when not auto-reconnecting). UI binds to this to surface the wait so the user knows what's happening (and can click Cancel during it)
    public string? ReconnectStatusText { get { lock (m_AutoReconnectLock) { return m_ReconnectStatusText; } } }
    #endregion

    #region Events
    public event EventHandler? StateChanged;
    public event EventHandler? ModeChanged;
    #endregion

    #region Ctors
    protected InsDeviceBase(DeviceDefinition i_Definition, DeviceConfig i_Config, ILogService i_LogService)
    {
        Definition = i_Definition;
        Config = i_Config;
        m_LogService = i_LogService;

        m_Status = DeviceStatus.Disconnected;
        m_LastError = null;
    }
    #endregion

    #region Functions
    // Connects to the device with a simulated connecting delay
    public async Task ConnectAsync()
    {
        CancellationToken token;

        lock (m_CtsLock)
        {
            if (m_Status == DeviceStatus.Connected || m_Status == DeviceStatus.Connecting)
            { return; }

            m_ConnectCts?.Cancel();
            m_ConnectCts?.Dispose();
            m_ConnectCts = new CancellationTokenSource();
            token = m_ConnectCts.Token;
        }

        SetStatus(DeviceStatus.Connecting, null);

        try
        {
            int delayMs = GetConnectDelayMs();
            await Task.Delay(delayMs, token);

            await OnConnectAsync();
            SetStatus(DeviceStatus.Connected, null);
        }
        catch (OperationCanceledException)
        {
            SetStatus(DeviceStatus.Disconnected, null);
        }
        catch (Exception ex)
        {
            SetStatus(DeviceStatus.Error, ex.Message);
        }
    }

    // Disconnects from the device
    public async Task DisconnectAsync()
    {
        lock (m_CtsLock)
        {
            if (m_Status == DeviceStatus.Disconnected) { return; }

            m_ConnectCts?.Cancel();
            m_ConnectCts?.Dispose();
            m_ConnectCts = null;
        }

        try
        {
            await OnDisconnectAsync();
            SetStatus(DeviceStatus.Disconnected, null);
        }
        catch (Exception ex)
        {
            SetStatus(DeviceStatus.Error, ex.Message);
        }
    }

    // Updates status and notifies listeners
    protected void SetStatus(DeviceStatus i_Status, string? i_Error)
    {
        if (m_Status == i_Status)
        { return; }

        DeviceStatus previous = m_Status;
        m_Status = i_Status;
        m_LastError = i_Error;

        switch (i_Status)
        {
            case DeviceStatus.Connected:
                m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} connected successfully");
                m_LastConnectedAtUtc = DateTime.UtcNow;
                break;

            case DeviceStatus.Disconnected:
                m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} disconnected");
                m_AutoReconnectAttempt = 0;  // user-initiated stop -- next failure starts fresh
                m_LastConnectedAtUtc = null;
                break;

            case DeviceStatus.Connecting:
                m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} connecting...");
                break;

            case DeviceStatus.Error:
                m_LogService.Error(nameof(InsDeviceBase), $"{Definition.DisplayName} error: {i_Error}");
                // If we just dropped from a Connected that was stable long enough, treat as a fresh problem and reset the counter. Short-lived Connecteds (flaps) keep the counter so backoff actually progresses 5 -> 10 -> 30 -> 60 instead of perma-5s
                if (previous == DeviceStatus.Connected && m_LastConnectedAtUtc.HasValue)
                {
                    TimeSpan connectedFor = DateTime.UtcNow - m_LastConnectedAtUtc.Value;
                    if (connectedFor.TotalMilliseconds >= c_StableConnectedResetMs)
                    {
                        m_AutoReconnectAttempt = 0;
                    }
                }
                m_LastConnectedAtUtc = null;
                break;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);

        // Auto-reconnect orchestration:
        //  - Connected -> Error means an unexpected drop (watchdog stall / sensor power loss / cable unplug). If AutoReconnect=true, start a backoff retry loop
        //  - Connecting -> Error means the initial connect failed; do NOT auto-retry (user-initiated; bad config should not retry forever)
        //  - Any transition to Connected or Disconnected cancels a pending retry loop (success or user said stop)
        if (previous == DeviceStatus.Connected && i_Status == DeviceStatus.Error && Config.AutoReconnect)
        {
            StartAutoReconnectLoop();
        }
        else if (i_Status == DeviceStatus.Connected || i_Status == DeviceStatus.Disconnected)
        {
            CancelAutoReconnectLoop();
        }
    }

    // Spawns the backoff retry loop. Idempotent -- if a loop is already running, this is a no-op so a noisy SetStatus storm doesn't fan out into multiple concurrent loops competing for ConnectAsync
    private void StartAutoReconnectLoop()
    {
        CancellationToken token;
        lock (m_AutoReconnectLock)
        {
            if (m_IsAutoReconnecting) { return; }
            m_AutoReconnectCts?.Dispose();
            m_AutoReconnectCts = new CancellationTokenSource();
            m_IsAutoReconnecting = true;
            token = m_AutoReconnectCts.Token;
        }
        m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} auto-reconnect started");
        StateChanged?.Invoke(this, EventArgs.Empty);
        _ = AutoReconnectLoopAsync(token);
    }

    // Cancels and clears a running loop. Called when status transitions to Connected (success -> stop trying), Disconnected (user-initiated stop), and from NotifyAutoReconnectChanged when the user toggles AutoReconnect off. Idempotent
    private void CancelAutoReconnectLoop()
    {
        bool wasRunning;
        lock (m_AutoReconnectLock)
        {
            wasRunning = m_IsAutoReconnecting || m_AutoReconnectCts != null;
            if (!wasRunning) { return; }
            m_AutoReconnectCts?.Cancel();
            m_AutoReconnectCts?.Dispose();
            m_AutoReconnectCts = null;
            m_IsAutoReconnecting = false;
            m_ReconnectStatusText = null;
        }
        if (wasRunning) { StateChanged?.Invoke(this, EventArgs.Empty); }
    }

    // Called by the UI when the user toggles AutoReconnect. If turned ON while in Error, kick off a fresh loop so the change takes effect immediately. If turned OFF, cancel any in-flight loop right now -- otherwise the loop would only check the flag at its next iteration (which could be up to 60s away mid-backoff)
    public void NotifyAutoReconnectChanged()
    {
        if (Config.AutoReconnect)
        {
            if (m_Status == DeviceStatus.Error) { StartAutoReconnectLoop(); }
        }
        else
        {
            CancelAutoReconnectLoop();
        }
    }

    // Backoff retry loop. Reads m_AutoReconnectAttempt (class-level, NOT loop-local) so the schedule progresses 5 -> 10 -> 30 -> 60 across loop respawns triggered by Connected->Error flaps. Tracks countdown text per-second so the UI can show "Reconnecting in 5s..." → "...4s..." → fire connect
    private async Task AutoReconnectLoopAsync(CancellationToken i_Token)
    {
        try
        {
            while (!i_Token.IsCancellationRequested)
            {
                int attemptForSchedule = m_AutoReconnectAttempt;
                int waitSec = s_AutoReconnectBackoffSec[Math.Min(attemptForSchedule, s_AutoReconnectBackoffSec.Length - 1)];
                m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} auto-reconnect attempt {attemptForSchedule + 1} scheduled in {waitSec}s");

                // Per-second countdown tick so the UI shows a live "Reconnecting in Ns..." instead of a frozen number that never moves. Each tick raises StateChanged so any binding to ReconnectStatusText re-evaluates
                for (int remaining = waitSec; remaining > 0; remaining--)
                {
                    if (i_Token.IsCancellationRequested) { return; }
                    SetReconnectStatusText($"Reconnecting in {remaining}s…");
                    try { await Task.Delay(c_CountdownTickMs, i_Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }

                if (i_Token.IsCancellationRequested) { return; }
                // Bail if the user disabled AutoReconnect mid-loop (the flag is read fresh, not snapshot at loop start)
                if (!Config.AutoReconnect) { m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} auto-reconnect cancelled (AutoReconnect turned off)"); return; }
                // If somehow we're no longer in Error (user clicked Connect manually, etc.), stop
                if (m_Status != DeviceStatus.Error) { return; }

                m_AutoReconnectAttempt++;
                SetReconnectStatusText("Reconnecting…");
                await ConnectAsync().ConfigureAwait(false);
                // On success: ConnectAsync -> SetStatus(Connected) -> CancelAutoReconnectLoop -> token cancelled, next Delay throws, we exit. m_LastConnectedAtUtc set; if the connect holds for 30s+, the NEXT drop resets the counter to 0; otherwise next loop spawns with our current counter
                // On failure: ConnectAsync -> SetStatus(Error). previous was Connecting (not Connected), so SetStatus does NOT spawn a second loop. We continue this loop's next iteration with the incremented counter
            }
        }
        finally
        {
            lock (m_AutoReconnectLock) { m_IsAutoReconnecting = false; m_ReconnectStatusText = null; }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Sets the countdown text under lock and pings StateChanged so x:Bind/Binding re-reads. Called once per second during the backoff wait
    private void SetReconnectStatusText(string i_Text)
    {
        lock (m_AutoReconnectLock) { m_ReconnectStatusText = i_Text; }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    protected void RaiseModeChanged()
    {
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    // Returns a connect delay (1–2 seconds)
    protected virtual int GetConnectDelayMs() { return 1000 + Random.Shared.Next(0, 1001); }

    // Performs device-specific connect logic
    protected abstract Task OnConnectAsync();

    // Performs device-specific disconnect logic
    protected abstract Task OnDisconnectAsync();
    #endregion
}