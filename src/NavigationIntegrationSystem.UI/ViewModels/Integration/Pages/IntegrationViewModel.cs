using Microsoft.UI.Dispatching;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Base;
using NavigationIntegrationSystem.UI.ViewModels.Devices;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Cards;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Pages;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Candidates;
using NavigationIntegrationSystem.UI.ViewModels.Integration.Layout;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration.Pages;

// Owns the Integration page state: header device toggles, per-row source selection, and dummy live updates
public sealed partial class IntegrationViewModel : ViewModelBase
{
    #region Private Fields
    private readonly DevicesViewModel m_DevicesViewModel;
    private readonly Random m_Rng = new();
    private readonly Dictionary<DeviceType, bool> m_DeviceVisibility = new();
    private DispatcherQueueTimer? m_Timer;
    #endregion

    #region Properties
    public ObservableCollection<IntegrationFieldRowViewModel> Rows { get; } = [];
    public ObservableCollection<IntegrationDeviceOptionViewModel> ConnectedDevices { get; } = [];
    #endregion

    #region Constructors
    public IntegrationViewModel(DevicesViewModel i_DevicesViewModel)
    {
        m_DevicesViewModel = i_DevicesViewModel;

        Rows.Add(new IntegrationFieldRowViewModel("Azimuth", "deg"));
        Rows.Add(new IntegrationFieldRowViewModel("Elevation", "deg"));
        Rows.Add(new IntegrationFieldRowViewModel("Latitude", "deg"));
        Rows.Add(new IntegrationFieldRowViewModel("Longitude", "deg"));
        Rows.Add(new IntegrationFieldRowViewModel("Altitude", "m"));
        Rows.Add(new IntegrationFieldRowViewModel("Pitch", "deg"));
        Rows.Add(new IntegrationFieldRowViewModel("Roll", "deg"));
        Rows.Add(new IntegrationFieldRowViewModel("Speed", "m/s"));

        HookDevices();
        RebuildFromConnectedDevices();
    }
    #endregion

    #region Functions
    // Starts dummy live updates using DispatcherQueue
    public void Initialize(DispatcherQueue i_DispatcherQueue)
    {
        if (m_Timer != null) { return; }

        m_Timer = i_DispatcherQueue.CreateTimer();
        m_Timer.Interval = TimeSpan.FromMilliseconds(250);
        m_Timer.Tick += OnTimerTick;
        m_Timer.Start();
    }

    // Called by header option when visibility changes
    public void OnHeaderVisibilityChanged(DeviceType i_DeviceType, bool i_IsVisible)
    {
        m_DeviceVisibility[i_DeviceType] = i_IsVisible;

        RefreshAllRowsVisibility();

        if (i_IsVisible) { TryAutoSelectNewlyVisibleSource(i_DeviceType); }
    }

    // Applies a selected source for all rows (by device type)
    public void ApplyDeviceToAllFields(DeviceType i_DeviceType)
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            IntegrationSourceCandidateViewModel? match = row.VisibleSources.FirstOrDefault(s => s.DeviceType == i_DeviceType);
            if (match != null) { row.SelectSource(match); }
        }
    }

    // Handles dummy telemetry tick
    private void OnTimerTick(DispatcherQueueTimer i_Sender, object i_Args) { Tick(); }

    // Updates dummy candidate values
    private void Tick()
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            double step = GetStepScale(row.Unit, row.FieldName);
            foreach (IntegrationSourceCandidateViewModel src in row.Sources) { src.Tick(step); }
        }
    }

    // Rebuilds header + per-row sources based on current connected devices
    private void RebuildFromConnectedDevices()
    {
        DeviceCardViewModel[] connected = m_DevicesViewModel.Devices.Where(d => d.Status == DeviceStatus.Connected).ToArray();

        RebuildHeaderDevices(connected);
        RebuildRowSources(connected);

        RefreshAllRowsVisibility();
    }

    // Rebuilds header options from connected devices and preserves toggle state
    private void RebuildHeaderDevices(DeviceCardViewModel[] i_Connected)
    {
        ConnectedDevices.Clear();

        foreach (DeviceCardViewModel device in i_Connected)
        {
            bool isVisible = m_DeviceVisibility.TryGetValue(device.Type, out bool cached) ? cached : true;

            var opt = new IntegrationDeviceOptionViewModel(device.Type, device.DisplayName, isVisible);

            opt.VisibilityChanged += (_, _) => OnHeaderVisibilityChanged(opt.DeviceType, opt.IsVisible);
            opt.ApplyToAllRequested += (_, _) => ApplyDeviceToAllFields(opt.DeviceType);

            ConnectedDevices.Add(opt);
            m_DeviceVisibility[device.Type] = isVisible;
        }
    }

    // Rebuilds each row candidate list and restores selection by device type
    private void RebuildRowSources(DeviceCardViewModel[] i_Connected)
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            DeviceType? previousDeviceType = row.SelectedDeviceType;

            row.Sources.Clear();
            foreach (DeviceCardViewModel device in i_Connected)
            {
                if (device.Type == DeviceType.Manual)
                {
                    row.Sources.Add(new ManualSourceCandidateViewModel(device.DisplayName));
                    continue;
                }

                double initial = CreateInitialValue(row.FieldName, row.Unit, device.Type);
                row.Sources.Add(new NumericSourceCandidateViewModel(device.Type, device.DisplayName, initial, m_Rng));
            }

            row.RestoreSelection(previousDeviceType);
        }
    }

    // Refreshes device visibility across all rows
    private void RefreshAllRowsVisibility()
    {
        foreach (IntegrationFieldRowViewModel row in Rows) { row.RefreshVisibleSources(IsCandidateVisible); }
    }

    // Candidate is visible only if its device is toggled visible in the header
    private bool IsCandidateVisible(IntegrationSourceCandidateViewModel i_Source)
    {
        return m_DeviceVisibility.TryGetValue(i_Source.DeviceType, out bool isVisible) && isVisible;
    }

    // Attempts to auto-select a newly visible device for rows that are currently "—"
    private void TryAutoSelectNewlyVisibleSource(DeviceType i_DeviceType)
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            if (!row.IsOutputEmpty) { continue; }

            IntegrationSourceCandidateViewModel? match = row.VisibleSources.FirstOrDefault(s => s.DeviceType == i_DeviceType);
            if (match != null) { row.SelectSource(match); }
        }
    }

    // Hooks device status changes (connect/disconnect) and keeps integration rebuilt
    private void HookDevices()
    {
        if (m_DevicesViewModel.Devices is INotifyCollectionChanged cc) { cc.CollectionChanged += OnDevicesCollectionChanged; }
        foreach (DeviceCardViewModel device in m_DevicesViewModel.Devices) { device.PropertyChanged += OnDevicePropertyChanged; }
    }

    // Hooks added/removed devices
    private void OnDevicesCollectionChanged(object? i_Sender, NotifyCollectionChangedEventArgs i_E)
    {
        if (i_E.NewItems != null)
        {
            foreach (DeviceCardViewModel device in i_E.NewItems) { device.PropertyChanged += OnDevicePropertyChanged; }
        }

        if (i_E.OldItems != null)
        {
            foreach (DeviceCardViewModel device in i_E.OldItems) { device.PropertyChanged -= OnDevicePropertyChanged; }
        }

        RebuildFromConnectedDevices();
    }

    // Rebuilds only when Status changes
    private void OnDevicePropertyChanged(object? i_Sender, PropertyChangedEventArgs i_E)
    {
        if (i_E.PropertyName != nameof(DeviceCardViewModel.Status)) { return; }
        RebuildFromConnectedDevices();
    }

    // Returns appropriate random step size per unit/field
    private double GetStepScale(string i_Unit, string i_FieldName)
    {
        if (i_Unit == "deg" && (i_FieldName == "Latitude" || i_FieldName == "Longitude")) { return 0.00005; }
        if (i_Unit == "m") { return 0.20; }
        if (i_Unit == "m/s") { return 0.15; }
        return 0.05;
    }

    // Creates a dummy initial value per field/device-type so candidates look distinct
    private double CreateInitialValue(string i_FieldName, string i_Unit, DeviceType i_DeviceType)
    {
        double baseValue =
            i_FieldName switch
            {
                "Azimuth" => 8.0,
                "Elevation" => 1.25,
                "Latitude" => 32.0853,
                "Longitude" => 34.7818,
                "Altitude" => 120.5,
                "Pitch" => 0.2,
                "Roll" => -0.1,
                "Speed" => 52.1,
                _ => 0.0
            };

        double deviceOffset = i_DeviceType == DeviceType.VN310 ? 0.0 : 5.0;
        return baseValue + deviceOffset;
    }
    #endregion
}