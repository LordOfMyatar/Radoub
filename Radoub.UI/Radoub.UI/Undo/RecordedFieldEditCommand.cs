using System;

namespace Radoub.UI.Undo;

/// <summary>
/// Records an edit that has <b>already been applied</b> to the model, so it can be undone as one
/// whole-field step. Unlike <see cref="SetFieldCommand{T}"/> (which captures the old value at
/// <see cref="Do"/>-time, for the "I am about to make this change" flow), this command is given an
/// explicit <paramref name="oldValue"/> and <paramref name="newValue"/>.
///
/// <para>This is the primitive for document/whole-field undo of TwoWay-bound fields (#2231): the
/// binding mutates the model as the user types, so by the time the host records the edit (on
/// focus-loss/Enter) the new value is already in place. <see cref="Do"/> re-applies the new value
/// (a harmless no-op when it is already set, and the correct action on redo); <see cref="Undo"/>
/// restores the whole previous committed value — it never reverts char-by-char or past the
/// captured baseline.</para>
/// </summary>
/// <typeparam name="T">Field value type.</typeparam>
public sealed class RecordedFieldEditCommand<T> : IUndoableCommand
{
    private readonly T _oldValue;
    private readonly T _newValue;
    private readonly Action<T> _setter;

    public RecordedFieldEditCommand(T oldValue, T newValue, Action<T> setter, string description)
    {
        _oldValue = oldValue;
        _newValue = newValue;
        _setter = setter ?? throw new ArgumentNullException(nameof(setter));
        Description = description ?? string.Empty;
    }

    public string Description { get; }

    public bool Do()
    {
        _setter(_newValue);
        return true;
    }

    public void Undo() => _setter(_oldValue);
}
