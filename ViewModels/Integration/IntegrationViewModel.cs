using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using NavigationIntegrationSystem.Core.Enums;
using NavigationIntegrationSystem.UI.ViewModels.Devices;
using System;
using System.Collections.ObjectModel;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Owns the Integration page state: connected device options, per-field row selection, and dummy live updates
public sealed partial class IntegrationViewModel : ObservableObject
{
    #region Private Fields
    private DispatcherQueueTimer? m_Timer;
    private readonly Random m_Rng;
    private readonly DevicesViewModel m_DevicesViewModel;
    private readonly ObservableCollection<IntegrationDeviceOptionViewModel> m_ConnectedDevices;
    #endregion

    #region Properties
    public ObservableCollection<IntegrationFieldRowViewModel> Rows { get; }
    public ObservableCollection<IntegrationDeviceOptionViewModel> ConnectedDevices => m_ConnectedDevices;
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
        var row = new IntegrationFieldRowViewModel(i_Field, i_Unit, new ObservableCollection<SourceCandidateViewModel>());

        BuildRowCandidates(row);
        row.SelectedSource = row.Sources.Count > 0 ? row.Sources[0] : null;
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

    // Rebuilds the header device options list from currently connected devices
    private void RebuildConnectedDevices()
    {
        m_ConnectedDevices.Clear();

        foreach (DeviceCardViewModel device in m_DevicesViewModel.Devices)
        {
            if (device.Status != DeviceStatus.Connected) { continue; }

            var opt = new IntegrationDeviceOptionViewModel(device.Type, device.DisplayName, true, ApplyDeviceToAllFields);
            opt.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IntegrationDeviceOptionViewModel.IsVisible)) { RefreshAllRowsVisibility(); }
            };

            m_ConnectedDevices.Add(opt);
        }
    }

    // Applies a selected device as the source for all fields (when that device exists in the row)
    private void ApplyDeviceToAllFields(DeviceType i_DeviceType)
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            foreach (SourceCandidateViewModel src in row.Sources)
            {
                if (src.DeviceType != i_DeviceType)
                { continue; }
                row.SelectedSource = src;
                break;
            }
        }
    }

    // Rebuilds the candidate list for a row based on currently connected devices
    private void BuildRowCandidates(IntegrationFieldRowViewModel i_Row)
    {
        i_Row.Sources.Clear();

        foreach (DeviceCardViewModel device in m_DevicesViewModel.Devices)
        {
            if (device.Status != DeviceStatus.Connected) { continue; }

            double initial = CreateInitialValue(i_Row.FieldName, i_Row.Unit, device.Type);
            i_Row.Sources.Add(new SourceCandidateViewModel(i_Row, device.Type, device.DisplayName, initial, m_Rng));
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
            if (dev.DeviceType == i_Source.DeviceType)
            { return dev.IsVisible; }
        }
        return false;
    }

    // Refreshes VisibleSources for every row and keeps SelectedSource valid
    private void RefreshAllRowsVisibility()
    {
        foreach (IntegrationFieldRowViewModel row in Rows)
        {
            row.RefreshVisibleSources(IsCandidateVisible);
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
                    row.RefreshVisibleSources(IsCandidateVisible);
                }
            };
        }
    }
    #endregion
}