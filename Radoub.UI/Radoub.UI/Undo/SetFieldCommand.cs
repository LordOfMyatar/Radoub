using System;

namespace Radoub.UI.Undo;

/// <summary>
/// Reversible property edit. Captures the current value at <see cref="Do"/> time (not
/// construction) so it restores correctly even if the field changed between construction and
/// execution. The workhorse for blueprint field edits (Reliquary #2295).
/// </summary>
/// <typeparam name="T">Field value type.</typeparam>
public sealed class SetFieldCommand<T> : IUndoableCommand
{
    private readonly Func<T> _getter;
    private readonly Action<T> _setter;
    private readonly T _newValue;
    private T _oldValue = default!;

    public SetFieldCommand(Func<T> getter, Action<T> setter, T newValue, string description)
    {
        _getter = getter ?? throw new ArgumentNullException(nameof(getter));
        _setter = setter ?? throw new ArgumentNullException(nameof(setter));
        _newValue = newValue;
        Description = description ?? string.Empty;
    }

    public string Description { get; }

    public void Do()
    {
        _oldValue = _getter();
        _setter(_newValue);
    }

    public void Undo() => _setter(_oldValue);
}
