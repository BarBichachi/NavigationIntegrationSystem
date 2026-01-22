using NavigationIntegrationSystem.Core.Devices;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models;
using NavigationIntegrationSystem.Devices.Config;
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
    private readonly ILogService m_LogService;
    #endregion

    #region Properties
    public DeviceDefinition Definition { get; }
    public DeviceStatus Status => m_Status;
    public string? LastError => m_LastError;
    public DeviceConfig Config { get; }
    #endregion

    #region Events
    public event EventHandler? StateChanged;
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
        if (m_Status == DeviceStatus.Connected || m_Status == DeviceStatus.Connecting)
        { return; }

        m_ConnectCts?.Cancel();
        m_ConnectCts?.Dispose();
        m_ConnectCts = new CancellationTokenSource();

        SetStatus(DeviceStatus.Connecting, null);

        try
        {
            int delayMs = GetConnectDelayMs();
            await Task.Delay(delayMs, m_ConnectCts.Token);

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
        if (m_Status == DeviceStatus.Disconnected)
        { return; }

        m_ConnectCts?.Cancel();
        m_ConnectCts?.Dispose();
        m_ConnectCts = null;

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
    }

    // Returns a connect delay (1–2 seconds)
    protected virtual int GetConnectDelayMs() { return 1000 + Random.Shared.Next(0, 1001); }

    // Performs device-specific connect logic
    protected abstract Task OnConnectAsync();

    // Performs device-specific disconnect logic
    protected abstract Task OnDisconnectAsync();
    #endregion
}