using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NavigationIntegrationSystem.UI.ViewModels.Base;

// Base class for ViewModels with INotifyPropertyChanged and SetProperty support
public abstract partial class ViewModelBase : INotifyPropertyChanged
{
    #region Events
    public event PropertyChangedEventHandler? PropertyChanged;
    #endregion

    #region Protected Helpers
    // Raises PropertyChanged for the specified property name
    protected void OnPropertyChanged([CallerMemberName] string? i_PropertyName = null)
    {
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
