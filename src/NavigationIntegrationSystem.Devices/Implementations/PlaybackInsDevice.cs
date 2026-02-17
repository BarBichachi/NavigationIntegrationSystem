using NavigationIntegrationSystem.Core.Logging;
using NavigationIntegrationSystem.Core.Models.Devices;
using NavigationIntegrationSystem.Core.Playback;
using NavigationIntegrationSystem.Devices.Models;
using NavigationIntegrationSystem.Devices.Runtime;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NavigationIntegrationSystem.Devices.Implementations;

public sealed class PlaybackInsDevice : InsDeviceBase
{
    #region Private Fields
    private readonly IPlaybackService m_PlaybackService;
    private readonly ConcurrentDictionary<string, double> m_LatestTelemetry = new();
    #endregion

    #region Properties
    // Exposes the live data for the Integration Service
    public IReadOnlyDictionary<string, double> Telemetry => m_LatestTelemetry;
    #endregion

    #region Constructors
    public PlaybackInsDevice(DeviceDefinition i_Definition, DeviceConfig i_Config, ILogService i_LogService, IPlaybackService i_PlaybackService)
        : base(i_Definition, i_Config, i_LogService)
    {
        m_PlaybackService = i_PlaybackService;
    }
    #endregion

    #region Functions
    // On connect, validate and load the playback file, set frequency, and subscribe to events
    protected override Task OnConnectAsync()
    {
        // 1. Validate File
        if (string.IsNullOrEmpty(Config.Connection.Playback.FilePath))
        {
            throw new InvalidOperationException("No playback file path configured.");
        }

        // 2. Load File (if changed or not loaded)
        if (m_PlaybackService.LoadedFilePath != Config.Connection.Playback.FilePath)
        {
            m_PlaybackService.LoadFileAsync(Config.Connection.Playback.FilePath).Wait();
        }

        // 3. Set Frequency
        m_PlaybackService.Frequency = Config.Connection.Playback.Frequency;

        // 4. Subscribe
        m_PlaybackService.PacketDispatched += OnPacketDispatched;
        m_PlaybackService.StateChanged += OnPlaybackStateChanged;

        return Task.CompletedTask;
    }

    // On disconnect, unsubscribe from events and optionally pause playback
    protected override Task OnDisconnectAsync()
    {
        m_PlaybackService.PacketDispatched -= OnPacketDispatched;
        m_PlaybackService.StateChanged -= OnPlaybackStateChanged;

        // Optionally pause playback when the device "disconnects"
        if (m_PlaybackService.IsPlaying)
        {
            m_PlaybackService.Pause();
        }

        m_PlaybackService.Unload();

        return Task.CompletedTask;
    }

    // Retrieve a value by key (used by Integration Service)
    public double GetValue(string i_Key)
    {
        return m_LatestTelemetry.TryGetValue(i_Key, out double val) ? val : 0.0;
    }
    #endregion

    #region Event Handlers
    // When a new packet is dispatched, update the local telemetry cache
    private void OnPacketDispatched(object? sender, PlaybackPacket e)
    {
        // Update local cache
        foreach (var kvp in e.Values)
        {
            m_LatestTelemetry[kvp.Key] = kvp.Value;
        }
    }

    // If the playback state changes and the file is unloaded, force disconnect to prevent stale data
    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        // If the file was unloaded externally, force disconnect
        if (Status == Core.Enums.DeviceStatus.Connected && string.IsNullOrEmpty(m_PlaybackService.LoadedFilePath))
        {
            _ = DisconnectAsync();
        }
    }
    #endregion
}