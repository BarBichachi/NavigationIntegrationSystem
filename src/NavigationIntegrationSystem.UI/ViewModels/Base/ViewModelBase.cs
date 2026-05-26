using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NavigationIntegrationSystem.UI.ViewModels.Base;

// Base class for ViewModels with INotifyPropertyChanged and SetProperty support. Includes a process-wide shutdown gate (SignalAppShutdown / s_IsAppShuttingDown) used by App.xaml.cs to silence all VM notifications before the host stops -- without this, any VM that fires PropertyChanged from a dispatcher-marshaled callback after WinUI has begun tearing down the view tree crashes with COMException (set on a dead DependencyObject) or AccessViolationException (cross-thread access to a half-disposed bind target). The gate is one volatile bool checked once per OnPropertyChanged call -- cost is negligible at normal rates and saves writing per-VM shutdown plumbing for every VM that subscribes to background-thread events
public abstract partial class ViewModelBase : INotifyPropertyChanged
{
    #region Static Shutdown Gate
    // Set to true by App.xaml.cs on MainWindow.Closed, before any await. Volatile so the close-thread write is visible to background threads that may be about to fire PropertyChanged via dispatcher callbacks
    private static volatile bool s_IsAppShuttingDown;

    public static void SignalAppShutdown()
    {
        s_IsAppShuttingDown = true;
    }
    #endregion

    #region Events
    public event PropertyChangedEventHandler? PropertyChanged;
    #endregion

    #region Protected Helpers
    // Raises PropertyChanged for the specified property name. No-op after SignalAppShutdown() so torn-down bindings can't be touched
    protected void OnPropertyChanged([CallerMemberName] string? i_PropertyName = null)
    {
        if (s_IsAppShuttingDown) { return; }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(i_PropertyName));
    }

    // Sets a backing field and raises PropertyChanged when the value changes
    protected bool SetProperty<T>(ref T io_Field, T i_Value, [CallerMemberName] string? i_PropertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(io_Field, i_Value)) { return false; }
        io_Field = i_Value;
        OnPropertyChanged(i_PropertyName);
        return true;
    }
    #endregion
}
