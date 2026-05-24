using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.Devices.Implementations.Vn310;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;

using System;
using System.Globalization;

namespace NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

// Bespoke inspect pane for VN310. Subscribes to Vn310InsDevice.TelemetryUpdated (raised from the SDK packet thread) and marshals refreshes to the UI thread. A DispatcherTimer ticks every 250ms to advance the "last packet age" + Hz figures between packets, so the inspect view feels live even when telemetry stops arriving
public sealed partial class Vn310InspectPaneViewModel : DeviceInspectPaneViewModelBase
{
    #region Private Fields
    private const int c_RefreshIntervalMs = 250;

    private readonly Vn310InsDevice m_Vn310Device;
    private readonly DispatcherQueue m_DispatcherQueue;
    private readonly DispatcherTimer m_RefreshTimer;

    private string m_UtcTimeText = c_NotAvailable;
    private string m_LatText = c_NotAvailable;
    private string m_LonText = c_NotAvailable;
    private string m_AltText = c_NotAvailable;
    private string m_YawText = c_NotAvailable;
    private string m_PitchText = c_NotAvailable;
    private string m_RollText = c_NotAvailable;
    private string m_YawRateText = c_NotAvailable;
    private string m_PitchRateText = c_NotAvailable;
    private string m_RollRateText = c_NotAvailable;
    private string m_VelNorthText = c_NotAvailable;
    private string m_VelEastText = c_NotAvailable;
    private string m_VelDownText = c_NotAvailable;
    private string m_SpeedText = c_NotAvailable;
    private string m_AttUncertaintyText = c_NotAvailable;
    private string m_PosUncertaintyText = c_NotAvailable;
    private string m_VelUncertaintyText = c_NotAvailable;

    private string m_InsStatusRawText = c_NotAvailable;
    private string m_InsModeText = c_NotAvailable;
    private string m_IsGpsFixText = c_NotAvailable;
    private string m_IsGpsHeadingInsText = c_NotAvailable;
    private string m_IsGpsCompassActiveText = c_NotAvailable;
    private string m_IsImuErrorText = c_NotAvailable;
    private string m_IsMagnetometerErrorText = c_NotAvailable;
    private string m_IsGnssErrorText = c_NotAvailable;

    private string m_TimeStatusRawText = c_NotAvailable;
    private string m_IsTimeOkText = c_NotAvailable;
    private string m_IsDateOkText = c_NotAvailable;
    private string m_IsUtcTimeValidText = c_NotAvailable;
    private string m_IsTimeStatusValidText = c_NotAvailable;

    private string m_PacketCountText = "0";
    private string m_PacketRateText = "—";
    private string m_LastPacketAgeText = "—";
    private string m_SourceModeText = "—";

    private bool m_IsAsciiMode;
    private bool m_IsBinaryMode;
    private bool m_HasAnyPacket;

    private bool m_IsNotConnectedBannerVisible;
    private string m_NotConnectedBannerText = string.Empty;

    private const string c_NotAvailable = "—";
    #endregion

    #region Properties
    public string UtcTimeText { get => m_UtcTimeText; private set => SetProperty(ref m_UtcTimeText, value); }
    public string LatText { get => m_LatText; private set => SetProperty(ref m_LatText, value); }
    public string LonText { get => m_LonText; private set => SetProperty(ref m_LonText, value); }
    public string AltText { get => m_AltText; private set => SetProperty(ref m_AltText, value); }
    public string YawText { get => m_YawText; private set => SetProperty(ref m_YawText, value); }
    public string PitchText { get => m_PitchText; private set => SetProperty(ref m_PitchText, value); }
    public string RollText { get => m_RollText; private set => SetProperty(ref m_RollText, value); }
    public string YawRateText { get => m_YawRateText; private set => SetProperty(ref m_YawRateText, value); }
    public string PitchRateText { get => m_PitchRateText; private set => SetProperty(ref m_PitchRateText, value); }
    public string RollRateText { get => m_RollRateText; private set => SetProperty(ref m_RollRateText, value); }
    public string VelNorthText { get => m_VelNorthText; private set => SetProperty(ref m_VelNorthText, value); }
    public string VelEastText { get => m_VelEastText; private set => SetProperty(ref m_VelEastText, value); }
    public string VelDownText { get => m_VelDownText; private set => SetProperty(ref m_VelDownText, value); }
    public string SpeedText { get => m_SpeedText; private set => SetProperty(ref m_SpeedText, value); }
    public string AttUncertaintyText { get => m_AttUncertaintyText; private set => SetProperty(ref m_AttUncertaintyText, value); }
    public string PosUncertaintyText { get => m_PosUncertaintyText; private set => SetProperty(ref m_PosUncertaintyText, value); }
    public string VelUncertaintyText { get => m_VelUncertaintyText; private set => SetProperty(ref m_VelUncertaintyText, value); }

    public string InsStatusRawText { get => m_InsStatusRawText; private set => SetProperty(ref m_InsStatusRawText, value); }
    public string InsModeText { get => m_InsModeText; private set => SetProperty(ref m_InsModeText, value); }
    public string IsGpsFixText { get => m_IsGpsFixText; private set => SetProperty(ref m_IsGpsFixText, value); }
    public string IsGpsHeadingInsText { get => m_IsGpsHeadingInsText; private set => SetProperty(ref m_IsGpsHeadingInsText, value); }
    public string IsGpsCompassActiveText { get => m_IsGpsCompassActiveText; private set => SetProperty(ref m_IsGpsCompassActiveText, value); }
    public string IsImuErrorText { get => m_IsImuErrorText; private set => SetProperty(ref m_IsImuErrorText, value); }
    public string IsMagnetometerErrorText { get => m_IsMagnetometerErrorText; private set => SetProperty(ref m_IsMagnetometerErrorText, value); }
    public string IsGnssErrorText { get => m_IsGnssErrorText; private set => SetProperty(ref m_IsGnssErrorText, value); }

    public string TimeStatusRawText { get => m_TimeStatusRawText; private set => SetProperty(ref m_TimeStatusRawText, value); }
    public string IsTimeOkText { get => m_IsTimeOkText; private set => SetProperty(ref m_IsTimeOkText, value); }
    public string IsDateOkText { get => m_IsDateOkText; private set => SetProperty(ref m_IsDateOkText, value); }
    public string IsUtcTimeValidText { get => m_IsUtcTimeValidText; private set => SetProperty(ref m_IsUtcTimeValidText, value); }
    public string IsTimeStatusValidText { get => m_IsTimeStatusValidText; private set => SetProperty(ref m_IsTimeStatusValidText, value); }

    public string PacketCountText { get => m_PacketCountText; private set => SetProperty(ref m_PacketCountText, value); }
    public string PacketRateText { get => m_PacketRateText; private set => SetProperty(ref m_PacketRateText, value); }
    public string LastPacketAgeText { get => m_LastPacketAgeText; private set => SetProperty(ref m_LastPacketAgeText, value); }
    public string SourceModeText { get => m_SourceModeText; private set => SetProperty(ref m_SourceModeText, value); }

    // ASCII banner is visible when we've received at least one packet and that packet was ASCII -- so the user understands why rates are dashed out
    public bool IsAsciiBannerVisible { get => m_IsAsciiMode && m_HasAnyPacket; }
    // Binary banner is visible when we've received at least one binary packet -- explains why uncertainty fields are dashed out (our subscribed binary groups don't include AttitudeGroup/InsGroup)
    public bool IsBinaryBannerVisible { get => m_IsBinaryMode && m_HasAnyPacket; }
    // Top-of-view "not connected" banner. Visible whenever the device isn't Connected; text differs based on whether we've ever seen telemetry (so user knows whether they're looking at stale data or no data at all)
    public bool IsNotConnectedBannerVisible { get => m_IsNotConnectedBannerVisible; private set => SetProperty(ref m_IsNotConnectedBannerVisible, value); }
    public string NotConnectedBannerText { get => m_NotConnectedBannerText; private set => SetProperty(ref m_NotConnectedBannerText, value); }
    #endregion

    #region Constructors
    public Vn310InspectPaneViewModel(DeviceCardViewModel i_Device) : base(i_Device)
    {
        m_Vn310Device = (Vn310InsDevice)i_Device.Device;
        m_DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        m_RefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(c_RefreshIntervalMs) };
        m_RefreshTimer.Tick += OnRefreshTimerTick;
        m_RefreshTimer.Start();

        m_Vn310Device.TelemetryUpdated += OnTelemetryUpdated;
        m_Vn310Device.StateChanged += OnDeviceStateChanged;

        // Seed the view with whatever telemetry already exists (so opening the pane after the device has been streaming for a while doesn't show all dashes until the next packet)
        Vn310Telemetry? seed = m_Vn310Device.LatestTelemetry;
        if (seed != null) { ApplyTelemetry(seed); }
        RefreshPacketStats();
        RefreshConnectionBanner();
    }
    #endregion

    #region Functions
    public override void Dispose()
    {
        m_RefreshTimer.Tick -= OnRefreshTimerTick;
        m_RefreshTimer.Stop();
        m_Vn310Device.TelemetryUpdated -= OnTelemetryUpdated;
        m_Vn310Device.StateChanged -= OnDeviceStateChanged;
    }

    // Populates all of the per-packet fields from a freshly received telemetry snapshot. Called both from OnTelemetryUpdated (after dispatcher hop) and during the constructor seed
    private void ApplyTelemetry(Vn310Telemetry i_Telemetry)
    {
        m_HasAnyPacket = true;
        m_IsBinaryMode = i_Telemetry.HasRates;
        m_IsAsciiMode = !i_Telemetry.HasRates;
        OnPropertyChanged(nameof(IsAsciiBannerVisible));
        OnPropertyChanged(nameof(IsBinaryBannerVisible));

        UtcTimeText = i_Telemetry.UtcTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        LatText = FormatDouble(i_Telemetry.LatDeg, "F6");
        LonText = FormatDouble(i_Telemetry.LonDeg, "F6");
        AltText = FormatDouble(i_Telemetry.AltM, "F2");
        YawText = FormatDouble(i_Telemetry.YawDeg, "F3");
        PitchText = FormatDouble(i_Telemetry.PitchDeg, "F3");
        RollText = FormatDouble(i_Telemetry.RollDeg, "F3");

        // Rates only available in binary mode. In ASCII mode show '—' so the user doesn't read zeros as "rotation rate is exactly zero"
        if (i_Telemetry.HasRates)
        {
            YawRateText = FormatDouble(i_Telemetry.YawRateDegS, "F3");
            PitchRateText = FormatDouble(i_Telemetry.PitchRateDegS, "F3");
            RollRateText = FormatDouble(i_Telemetry.RollRateDegS, "F3");
        }
        else
        {
            YawRateText = c_NotAvailable;
            PitchRateText = c_NotAvailable;
            RollRateText = c_NotAvailable;
        }

        VelNorthText = FormatDouble(i_Telemetry.VelNorth, "F3");
        VelEastText = FormatDouble(i_Telemetry.VelEast, "F3");
        VelDownText = FormatDouble(i_Telemetry.VelDown, "F3");
        SpeedText = FormatDouble(i_Telemetry.Speed, "F3");

        // Uncertainties only populated in ASCII mode. Binary path has them as 0f because we don't subscribe to AttitudeGroup/InsGroup (Phase 7 may extend this); show '—' for binary mode
        if (i_Telemetry.HasRates)
        {
            AttUncertaintyText = c_NotAvailable;
            PosUncertaintyText = c_NotAvailable;
            VelUncertaintyText = c_NotAvailable;
        }
        else
        {
            AttUncertaintyText = FormatFloat(i_Telemetry.AttUncertainty, "F4");
            PosUncertaintyText = FormatFloat(i_Telemetry.PosUncertainty, "F4");
            VelUncertaintyText = FormatFloat(i_Telemetry.VelUncertainty, "F4");
        }

        InsStatusRawText = $"0x{i_Telemetry.InsStatus.RawData:X4}";
        InsModeText = i_Telemetry.InsStatus.Mode.ToString();
        IsGpsFixText = FormatBool(i_Telemetry.InsStatus.IsGpsFix);
        IsGpsHeadingInsText = FormatBool(i_Telemetry.InsStatus.IsGpsHeadingIns);
        IsGpsCompassActiveText = FormatBool(i_Telemetry.InsStatus.IsGpsCompassActive);
        IsImuErrorText = FormatBool(i_Telemetry.InsStatus.InsErrors.IsImuError);
        IsMagnetometerErrorText = FormatBool(i_Telemetry.InsStatus.InsErrors.IsMagnetometerError);
        IsGnssErrorText = FormatBool(i_Telemetry.InsStatus.InsErrors.IsGnssError);

        // Time Status only present in binary packets. ASCII VNINS doesn't carry it, so show '—' so the user knows the section isn't applicable
        if (i_Telemetry.HasRates)
        {
            TimeStatusRawText = $"0x{i_Telemetry.TimeStatus.RawData:X2}";
            IsTimeOkText = FormatBool(i_Telemetry.TimeStatus.IsTimeOK);
            IsDateOkText = FormatBool(i_Telemetry.TimeStatus.IsDateOK);
            IsUtcTimeValidText = FormatBool(i_Telemetry.TimeStatus.IsUtcTimeValid);
            IsTimeStatusValidText = FormatBool(i_Telemetry.TimeStatus.IsValid);
        }
        else
        {
            TimeStatusRawText = c_NotAvailable;
            IsTimeOkText = c_NotAvailable;
            IsDateOkText = c_NotAvailable;
            IsUtcTimeValidText = c_NotAvailable;
            IsTimeStatusValidText = c_NotAvailable;
        }
    }

    // Updates the packet-stats section. Called on every timer tick AND right after a packet, so the count + Hz move with the data
    private void RefreshPacketStats()
    {
        long count = m_Vn310Device.PacketCount;
        PacketCountText = count.ToString(CultureInfo.InvariantCulture);

        Vn310PacketSourceMode mode = m_Vn310Device.LastSourceMode;
        SourceModeText = mode == Vn310PacketSourceMode.Unknown ? c_NotAvailable : mode.ToString();

        DateTime[] timestamps = m_Vn310Device.GetRecentPacketTimestamps();
        if (timestamps.Length == 0)
        {
            PacketRateText = c_NotAvailable;
            LastPacketAgeText = c_NotAvailable;
            return;
        }

        DateTime now = DateTime.UtcNow;
        DateTime cutoff = now - TimeSpan.FromSeconds(1);
        int recentCount = 0;
        for (int i = 0; i < timestamps.Length; i++)
        {
            if (timestamps[i] >= cutoff) { recentCount++; }
        }
        PacketRateText = $"{recentCount} Hz";

        DateTime newest = timestamps[timestamps.Length - 1];
        double ageMs = (now - newest).TotalMilliseconds;
        LastPacketAgeText = $"{ageMs:F0} ms";
    }

    private static string FormatDouble(double i_Value, string i_Format)
    {
        return i_Value.ToString(i_Format, CultureInfo.InvariantCulture);
    }

    private static string FormatFloat(float i_Value, string i_Format)
    {
        return i_Value.ToString(i_Format, CultureInfo.InvariantCulture);
    }

    private static string FormatBool(bool i_Value)
    {
        return i_Value ? "yes" : "no";
    }

    // Recomputes the "not connected" banner state from device Status + whether any packet has arrived. Hidden when Connected, otherwise text varies: "no data yet" vs "showing last received values"
    private void RefreshConnectionBanner()
    {
        DeviceStatus status = m_Vn310Device.Status;
        bool connected = status == DeviceStatus.Connected;
        IsNotConnectedBannerVisible = !connected;
        if (connected) { return; }

        NotConnectedBannerText = m_HasAnyPacket
            ? "Device not connected — showing last received values."
            : "Device not connected — connect to receive telemetry.";
    }
    #endregion

    #region Event Handlers
    // Packet arrival comes from the SDK's read thread; marshal to UI before touching observable properties (which fire INPC that x:Bind processes only on the UI thread)
    private void OnTelemetryUpdated(object? i_Sender, Vn310Telemetry i_Telemetry)
    {
        if (m_DispatcherQueue.HasThreadAccess)
        {
            ApplyTelemetry(i_Telemetry);
            RefreshPacketStats();
        }
        else
        {
            m_DispatcherQueue.TryEnqueue(() => { ApplyTelemetry(i_Telemetry); RefreshPacketStats(); });
        }
    }

    // Device status transitions (Connecting → Connected, Connected → Disconnected, watchdog → Error) drive the "not connected" banner. Like StateChanged elsewhere, can fire from background threads (watchdog timer); marshal to UI
    private void OnDeviceStateChanged(object? i_Sender, EventArgs i_Args)
    {
        if (m_DispatcherQueue.HasThreadAccess)
        {
            RefreshConnectionBanner();
        }
        else
        {
            m_DispatcherQueue.TryEnqueue(RefreshConnectionBanner);
        }
    }

    // Already on the UI thread (DispatcherTimer fires there); refresh the stats so "last packet age" creeps up between packets and Hz stays current even if the wire goes quiet
    private void OnRefreshTimerTick(object? i_Sender, object i_Args)
    {
        RefreshPacketStats();
    }
    #endregion
}
