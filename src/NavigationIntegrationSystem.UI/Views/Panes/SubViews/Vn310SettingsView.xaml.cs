using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationIntegrationSystem.UI.ViewModels.Devices.Panes;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;

namespace NavigationIntegrationSystem.UI.Views.Panes.SubViews;

// VN310-specific connection settings pane. Differs from RealDeviceSettingsView in two ways: the Connection-kind selector + UDP/TCP sections are gone (VN310 is serial-only), and the COM Port input is a dropdown sourced from SerialPort.GetPortNames() with a Refresh button instead of a free-text TextBox
public sealed partial class Vn310SettingsView : UserControl
{
    #region Private Fields
    private readonly ObservableCollection<string> m_ComPorts = new ObservableCollection<string>();
    #endregion

    #region Properties
    public Vn310SettingsPaneViewModel ViewModel => (Vn310SettingsPaneViewModel)DataContext;
    // Bound by the COM Port ComboBox's ItemsSource. Refreshed on Loaded + DataContext change + manual Refresh-button click
    public ObservableCollection<string> ComPorts => m_ComPorts;

    // Non-empty when Draft.SerialComPort holds a value that is NOT in the currently-detected port list (e.g. user previously saved COM7 but the USB-serial cable is unplugged). Bound by a TextBlock under the COM Port dropdown so the user knows their saved value is stale instead of being confused why Connect rejects it
    public string SavedPortMissingMessage
    {
        get
        {
            string saved = ViewModel?.Draft?.SerialComPort ?? string.Empty;
            if (string.IsNullOrWhiteSpace(saved)) { return string.Empty; }
            if (m_ComPorts.Contains(saved, StringComparer.OrdinalIgnoreCase)) { return string.Empty; }
            return $"Saved port \"{saved}\" is not currently detected";
        }
    }
    #endregion

    #region Constructors
    public Vn310SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }
    #endregion

    #region Functions
    // Re-enumerates SerialPort.GetPortNames() into m_ComPorts. The dropdown only ever shows ports the OS reports as present; a stale saved port (e.g. USB-serial currently unplugged) is surfaced via SavedPortMissingMessage below the dropdown instead of being injected into the list. Diff-and-patch (rather than Clear+AddAll) avoids transient SelectedItem flicker
    private void RefreshComPorts()
    {
        List<string> desired = SerialPort.GetPortNames().ToList();
        desired.Sort(StringComparer.OrdinalIgnoreCase);

        for (int i = m_ComPorts.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(m_ComPorts[i], StringComparer.OrdinalIgnoreCase))
            {
                m_ComPorts.RemoveAt(i);
            }
        }
        foreach (string port in desired)
        {
            if (!m_ComPorts.Contains(port, StringComparer.OrdinalIgnoreCase))
            {
                m_ComPorts.Add(port);
            }
        }

        SyncComPortSelection();

        // SavedPortMissingMessage depends on (m_ComPorts vs Draft.SerialComPort); both can change here, so re-evaluate
        Bindings.Update();
    }

    // Manually drives ComPortComboBox.SelectedItem from Draft.SerialComPort. Sets to null when the saved value isn't in the detected list so the dropdown reads as honestly unset (instead of WinUI's default behavior of visually showing the first item while internally retaining the unresolvable value)
    private void SyncComPortSelection()
    {
        string saved = ViewModel?.Draft?.SerialComPort ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(saved) && m_ComPorts.Contains(saved, StringComparer.OrdinalIgnoreCase))
        {
            // Walk m_ComPorts to find the case-insensitive match -- the ObservableCollection stores whatever case GetPortNames returned, which may differ from Draft's stored casing
            foreach (string port in m_ComPorts)
            {
                if (string.Equals(port, saved, StringComparison.OrdinalIgnoreCase))
                {
                    ComPortComboBox.SelectedItem = port;
                    return;
                }
            }
        }

        ComPortComboBox.SelectedItem = null;
    }
    #endregion

    #region Event Handlers
    private void OnLoaded(object i_Sender, RoutedEventArgs i_Args)
    {
        RefreshComPorts();
    }

    // Mirror of RealDeviceSettingsView: x:Bind compiles against ViewModel; when DataContext switches to a different device's VM, force-re-evaluate so the new VM's values render. Re-enumerate ports too because the saved port may differ per-device
    private void OnDataContextChanged(FrameworkElement i_Sender, DataContextChangedEventArgs i_Args)
    {
        if (i_Args.NewValue is Vn310SettingsPaneViewModel)
        {
            Bindings.Update();
            RefreshComPorts();
        }
    }

    private void OnRefreshComPortsClick(object i_Sender, RoutedEventArgs i_Args)
    {
        RefreshComPorts();
    }

    // Manual write-back for the COM port ComboBox (which is OneWay-bound to Draft.SerialComPort, not TwoWay). Only propagates the change when the new selection actually differs from the current draft value; this skips no-op SelectionChanged events fired during programmatic SelectedItem updates and prevents spuriously dirtying the draft when the pane is collapsed for a non-VN310 device
    private void OnComPortSelectionChanged(object i_Sender, SelectionChangedEventArgs i_Args)
    {
        if (i_Sender is not ComboBox comboBox) { return; }
        if (comboBox.SelectedItem is not string newPort) { return; }
        if (ViewModel?.Draft == null) { return; }
        if (string.Equals(ViewModel.Draft.SerialComPort, newPort, StringComparison.OrdinalIgnoreCase)) { return; }

        ViewModel.Draft.SerialComPort = newPort;
        // SavedPortMissingMessage depends on Draft.SerialComPort; the user just picked a real (detected) port so the warning should clear
        Bindings.Update();
    }
    #endregion
}
