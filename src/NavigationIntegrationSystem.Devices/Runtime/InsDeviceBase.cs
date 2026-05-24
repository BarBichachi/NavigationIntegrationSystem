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
    private readonly object m_AutoReconnectLock = new object();
    // Backoff schedule (seconds). Caps at the last value -- after 16s we retry every 30s indefinitely until the device reconnects, the user clicks Disconnect, or AutoReconnect is turned off. Total: 1, 2, 4, 8, 16, then 30s repeating
    private static readonly int[] s_AutoReconnectBackoffSec = { 1, 2, 4, 8, 16, 30 };
    #endregion

    #region Properties
    public DeviceDefinition Definition { get; }
    public DeviceStatus Status => m_Status;
    public string? LastError => m_LastError;
    public DeviceConfig Config { get; }
    public virtual DeviceModeSnapshot? CurrentMode => null;
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
                break;

            case DeviceStatus.Disconnected:
                m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} disconnected");
                break;

            case DeviceStatus.Connecting:
                m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} connecting...");
                break;

            case DeviceStatus.Error:
                m_LogService.Error(nameof(InsDeviceBase), $"{Definition.DisplayName} error: {i_Error}");
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
        _ = AutoReconnectLoopAsync(token);
    }

    // Cancels and clears a running loop. Called when status transitions to Connected (success -> stop trying) or Disconnected (user-initiated stop). Idempotent
    private void CancelAutoReconnectLoop()
    {
        lock (m_AutoReconnectLock)
        {
            if (!m_IsAutoReconnecting && m_AutoReconnectCts == null) { return; }
            m_AutoReconnectCts?.Cancel();
            m_AutoReconnectCts?.Dispose();
            m_AutoReconnectCts = null;
            m_IsAutoReconnecting = false;
        }
    }

    // Backoff retry loop. Waits per schedule, then ConnectAsync. ConnectAsync handles its own status transitions; this loop just keeps trying until the token is cancelled (via SetStatus -> Connected/Disconnected) or AutoReconnect is turned off mid-flight
    private async Task AutoReconnectLoopAsync(CancellationToken i_Token)
    {
        int attempt = 0;
        try
        {
            while (!i_Token.IsCancellationRequested)
            {
                int waitSec = s_AutoReconnectBackoffSec[Math.Min(attempt, s_AutoReconnectBackoffSec.Length - 1)];
                m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} auto-reconnect attempt {attempt + 1} scheduled in {waitSec}s");
                try { await Task.Delay(TimeSpan.FromSeconds(waitSec), i_Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }

                if (i_Token.IsCancellationRequested) { return; }
                // Bail if the user disabled AutoReconnect mid-loop (the flag is read fresh, not snapshot at loop start)
                if (!Config.AutoReconnect) { m_LogService.Info(nameof(InsDeviceBase), $"{Definition.DisplayName} auto-reconnect cancelled (AutoReconnect turned off)"); return; }
                // If somehow we're no longer in Error (user clicked Connect manually, etc.), stop
                if (m_Status != DeviceStatus.Error) { return; }

                attempt++;
                await ConnectAsync().ConfigureAwait(false);
                // On success: ConnectAsync -> SetStatus(Connected) -> CancelAutoReconnectLoop -> token cancelled, next Delay throws, we exit
                // On failure: ConnectAsync -> SetStatus(Error). previous was Connecting (not Connected), so SetStatus does NOT spawn a second loop. We continue this loop's next iteration
            }
        }
        finally
        {
            lock (m_AutoReconnectLock) { m_IsAutoReconnecting = false; }
        }
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