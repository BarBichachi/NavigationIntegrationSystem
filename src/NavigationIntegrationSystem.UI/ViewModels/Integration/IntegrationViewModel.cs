using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Devices;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Owns the Integration page state: connected device options, per-field row selection, and dummy live updates
public sealed partial class IntegrationViewModel : ObservableObject
{
    #region Private Fields
    private DispatcherQueueTimer? m_Timer;
    private readonly Random m_Rng;
    private readonly DevicesViewModel m_DevicesViewModel;
    private readonly ObservableCollection<IntegrationDeviceOptionViewModel> m_ConnectedDevices;
    private bool m_IsManualVisible = true;
    #endregion

    #region Properties
    public ObservableCollection<IntegrationFieldRowViewModel> Rows { get; }
    public ObservableCollection<IntegrationDeviceOptionViewModel> ConnectedDevices => m_ConnectedDevices;
    public bool IsManualVisible { get => m_IsManualVisible; set { if (SetProperty(ref m_IsManualVisible, value)) { RefreshAllRowsVisibility(); } } }
    #endregion

    #region Constructors
    public IntegrationViewModel(DevicesViewModel i_DevicesViewModel)
    {
        m_Rng = new Random();
        m_DevicesViewModel = i_DevicesViewModel;
        m_ConnectedDevices = new ObservableCollection<IntegrationDeviceOptionViewModel>();

        Rows =
        [
            CreateRow("Azimuth", "deg"),
            CreateRow("Elevation", "deg"),
            CreateRow("Latitude", "deg"),
            CreateRow("Longitude", "deg"),
            CreateRow("Altitude", "m"),
            CreateRow("Pitch", "deg"),
            CreateRow("Roll", "deg"),
            CreateRow("Speed", "m/s"),
        ];

        HookDeviceConnectionNotifications();
        RebuildConnectedDevices();
        RefreshAllRowsVisibility();
    }
    #endregion

    #region Functions
    // Starts the dummy live updates using the UI dispatcher
    public void Initialize(DispatcherQueue i_DispatcherQueue)
    {
        if (m_Timer != null) { return; }

        m_Timer = i_DispatcherQueue.CreateTimer();
        m_Timer.Interval = TimeSpan.FromMilliseconds(250);
        m_Timer.Tick += (_, _) => OnTick();
        m_Timer.Start();
    }

    // Creates a single row and builds its candidate sources from currently connected devices
    private IntegrationFieldRowViewModel CreateRow(string i_Field, string i_Unit)
    {
        // Pass m_Rng to the new constructor signature
        var row = new IntegrationFieldRowViewModel(this, i_Field, i_Unit, m_Rng);

        BuildRowCandidates(row);
        row.RefreshVisibleSources(IsCandidateVisible);

        return row;
    }

    // Updates all candidate values so the UI proves live interaction works
    private void OnTick()
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            double step = GetStepScale(row.Unit, row.FieldName);
            foreach (SourceCandidateViewModel src in row.Sources)
            {
                src.Tick(step);
            }
        }
    }

    // Returns an appropriate random step size per unit/field
    private double GetStepScale(string i_Unit, string i_FieldName)
    {
        if (i_Unit == "deg" && (i_FieldName == "Latitude" || i_FieldName == "Longitude")) { return 0.00005; }
        if (i_Unit == "m") { return 0.20; }
        if (i_Unit == "m/s") { return 0.15; }
        return 0.05;
    }

    // Rebuilds the header device options list and hooks auto-selection logic
    private void RebuildConnectedDevices()
    {
        m_ConnectedDevices.Clear();

        foreach (DeviceCardViewModel device in m_DevicesViewModel.Devices)
        {
            if (device.Status != DeviceStatus.Connected) { continue; }

            var opt = new IntegrationDeviceOptionViewModel(device.Type, device.DisplayName, true, ApplyDeviceToAllFields);
            opt.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IntegrationDeviceOptionViewModel.IsVisible))
                {
                    HandleSourceVisibilityChanged(opt);
                }
            };

            m_ConnectedDevices.Add(opt);
        }
    }

    // Refreshes UI visibility and automatically selects a source if it just became visible
    private void HandleSourceVisibilityChanged(IntegrationDeviceOptionViewModel i_Option)
    {
        RefreshAllRowsVisibility();

        // If a source becomes visible, automatically select it for all rows
        if (i_Option.IsVisible)
        {
            TryAutoSelectSource(i_Option.DeviceType);
        }
    }

    // Attempts to select a device for all rows only if the row is currently displaying "-"
    private void TryAutoSelectSource(DeviceType i_DeviceType)
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            if (row.SelectedValueText != "—") { continue; }

            ApplySourceToRow(row, i_DeviceType);
        }
    }

    // Applies a selected device (or manual mode) as the source for all integration fields
    public void ApplyDeviceToAllFields(DeviceType i_DeviceType)
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            ApplySourceToRow(row, i_DeviceType);
        }
    }

    // Sub-function to handle the selection logic for a specific row
    private void ApplySourceToRow(IntegrationFieldRowViewModel i_Row, DeviceType i_DeviceType)
    {
        // Check if we are applying the Manual source first
        if (i_DeviceType == DeviceType.Manual)
        {
            i_Row.ManualSource.ForceSelect();
            return;
        }

        // Otherwise, look for the matching hardware device in the visible collection
        SourceCandidateViewModel? target = i_Row.VisibleSources.FirstOrDefault(src => src.DeviceType == i_DeviceType);

        if (target != null) { target.ForceSelect(); }
    }

    // Rebuilds the candidate list for a row based on currently connected devices
    private void BuildRowCandidates(IntegrationFieldRowViewModel i_Row)
    {
        DeviceType? previousType = i_Row.SelectedSource?.DeviceType;

        i_Row.Sources.Clear();
        PopulateDeviceSources(i_Row);
        i_Row.RefreshVisibleSources(IsCandidateVisible);

        RestoreSelection(i_Row, previousType);
    }

    // Iterates through connected hardware devices to add them as candidates
    private void PopulateDeviceSources(IntegrationFieldRowViewModel i_Row)
    {
        foreach (DeviceCardViewModel device in m_DevicesViewModel.Devices)
        {
            if (device.Status != DeviceStatus.Connected) { continue; }

            double initial = CreateInitialValue(i_Row.FieldName, i_Row.Unit, device.Type);
            var candidate = new SourceCandidateViewModel(i_Row, device.Type, device.DisplayName, initial, m_Rng);

            // Ensure new candidate is logically deselected before adding to collection
            candidate.NotifySelectionChanged(false);

            i_Row.Sources.Add(candidate);
        }
    }

    // Attempts to re-select the previously chosen source type or defaults to Manual
    private void RestoreSelection(IntegrationFieldRowViewModel i_Row, DeviceType? i_PreviousType)
    {
        SourceCandidateViewModel? next = i_Row.VisibleSources.FirstOrDefault(s => s.DeviceType == i_PreviousType);

        if (next != null)
        {
            next.IsSelected = true;
        }
        else if (i_PreviousType != DeviceType.Manual)
        {
            // Fallback to manual if the specific device disappeared
            i_Row.ManualSource.IsSelected = true;
            i_Row.UpdateSelection(i_Row.ManualSource);
        }
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

        double deviceOffset = i_DeviceType == DeviceType.VN310 ? 0.0 : 0.15;
        return baseValue + deviceOffset;
    }

    // Returns true only if the candidate device is currently enabled in the header toggles
    private bool IsCandidateVisible(SourceCandidateViewModel i_Source)
    {
        foreach (IntegrationDeviceOptionViewModel dev in m_ConnectedDevices)
        {
            if (dev.DeviceType == i_Source.DeviceType) { return dev.IsVisible; }
        }
        return false;
    }

    // Refreshes VisibleSources for every row and keeps SelectedSource valid
    private void RefreshAllRowsVisibility()
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            row.NotifyManualVisibilityChanged();
            row.RefreshVisibleSources(IsCandidateVisible);
            row.HandleVisibilityFallback();
            row.ReassertVisualSelection();
            row.RefreshIntegratedOutput();
        }
    }

    // Hooks Status changes so Integration reacts to connect/disconnect events
    private void HookDeviceConnectionNotifications()
    {
        foreach (DeviceCardViewModel device in m_DevicesViewModel.Devices)
        {
            device.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(DeviceCardViewModel.Status)) { return; }

                RebuildConnectedDevices();

                foreach (IntegrationFieldRowViewModel row in Rows)
                {
                    BuildRowCandidates(row);
                }
            };
        }
    }
    #endregion
}