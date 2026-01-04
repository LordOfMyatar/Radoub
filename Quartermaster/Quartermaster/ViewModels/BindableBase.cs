using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Quartermaster.ViewModels;

/// <summary>
/// Base class implementing INotifyPropertyChanged with SetProperty helper.
/// Reduces boilerplate in view models.
/// </summary>
public abstract class BindableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets the property value and raises PropertyChanged if changed.
    /// </summary>
    /// <returns>True if value changed, false otherwise.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
