using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlaceableEditor.ViewModels;

/// <summary>
/// One placeable event-handler slot (e.g. OnOpen → "my_onopen"). Reads/writes the script ResRef
/// straight through to the wrapped <see cref="Radoub.Formats.Utp.UtpFile"/> via the supplied
/// accessor delegates, so there is no separate copy to keep in sync.
/// </summary>
public sealed class ScriptSlotViewModel : INotifyPropertyChanged
{
    private readonly Func<string> _get;
    private readonly Action<string> _set;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <param name="eventName">Engine event name, e.g. "OnOpen".</param>
    /// <param name="label">Display label for the row, e.g. "On Open".</param>
    public ScriptSlotViewModel(string eventName, string label, Func<string> getter, Action<string> setter)
    {
        EventName = eventName;
        Label = label;
        _get = getter ?? throw new ArgumentNullException(nameof(getter));
        _set = setter ?? throw new ArgumentNullException(nameof(setter));
    }

    /// <summary>Engine event name (stable id, used by tests + script-set presets).</summary>
    public string EventName { get; }

    /// <summary>Human-readable row label.</summary>
    public string Label { get; }

    /// <summary>The assigned script ResRef (≤16 chars). Writes through to the model.</summary>
    public string ResRef
    {
        get => _get();
        set
        {
            if (_get() == value) return;
            _set(value ?? string.Empty);
            OnPropertyChanged();
        }
    }

    /// <summary>Re-raise PropertyChanged so the grid rebinds after a model reload or undo.</summary>
    public void Refresh() => OnPropertyChanged(nameof(ResRef));

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
