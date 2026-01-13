using System;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.UI.Dispatching;

namespace NavigationIntegrationSystem.UI.ViewModels.Integration;

// Drives the Fusion grid rows and simulates live source values for UI validation
public sealed partial class IntegrationViewModel : ObservableObject
{
    #region Private Fields
    private DispatcherQueueTimer? m_Timer;
    private readonly Random m_Rng;
    #endregion

    #region Properties
    public ObservableCollection<IntegrationFieldRowViewModel> Rows { get; }
    #endregion

    #region Constructors
    public IntegrationViewModel()
    {
        m_Rng = new Random();

        Rows =
        [
            CreateRow("Azimuth", "deg", 7.89898, 9.09090, 10.12031),
            CreateRow("Elevation", "deg", 1.25000, 1.18000, 1.31000),
            CreateRow("Latitude", "deg", 32.08530, 32.08510, 32.08560),
            CreateRow("Longitude", "deg", 34.78180, 34.78140, 34.78195),
            CreateRow("Altitude", "m", 120.50, 121.10, 119.90),
            CreateRow("Pitch", "deg", 0.20, 0.15, 0.24),
            CreateRow("Roll", "deg", -0.10, -0.08, -0.12),
            CreateRow("Speed", "m/s", 52.10, 51.80, 52.50),
        ];
    }
    #endregion

    #region Private Functions
    // Creates one fusion row with 3 candidate sources (A/B/C)
    private IntegrationFieldRowViewModel CreateRow(string i_Field, string i_Unit, double i_A, double i_B, double i_C)
    {
        var row = new IntegrationFieldRowViewModel(i_Field, i_Unit, new ObservableCollection<SourceCandidateViewModel>());
        row.Sources.Add(new SourceCandidateViewModel(row, "VN310", 0, i_A, m_Rng));
        row.Sources.Add(new SourceCandidateViewModel(row, "Tmaps100X", 1, i_B, m_Rng));
        row.Sources.Add(new SourceCandidateViewModel(row, "SomeINS", 2, i_C, m_Rng));
        row.SelectedSource = row.Sources[0];

        return row;
    }

    // Ticks dummy values so UI proves the live interaction works
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

    // Keeps movement realistic-ish per field/unit without overthinking it
    private double GetStepScale(string i_Unit, string i_FieldName)
    {
        if (i_Unit == "deg" && (i_FieldName == "Latitude" || i_FieldName == "Longitude"))
        { return 0.00005; }
        if (i_Unit == "m")
        { return 0.20; }
        if (i_Unit == "m/s")
        { return 0.15; }
        return 0.05;
    }

    // Starts the dummy live updates using the UI dispatcher
    public void Initialize(DispatcherQueue i_DispatcherQueue)
    {
        if (m_Timer != null) { return; }

        m_Timer = i_DispatcherQueue.CreateTimer();
        m_Timer.Interval = TimeSpan.FromMilliseconds(250);
        m_Timer.Tick += (_, _) => OnTick();
        m_Timer.Start();
    }

    #endregion
}