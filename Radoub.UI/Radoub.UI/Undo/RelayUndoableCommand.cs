using System;

namespace Radoub.UI.Undo;

/// <summary>
/// An <see cref="IUndoableCommand"/> backed by two delegates. Use for one-off mutations that
/// don't warrant a dedicated command class. For simple property edits prefer
/// <see cref="SetFieldCommand{T}"/>, which captures the old value automatically.
/// </summary>
public sealed class RelayUndoableCommand : IUndoableCommand
{
    private readonly Func<bool> _do;
    private readonly Action _undo;

    /// <summary>
    /// Create a command whose Do action always succeeds (the common case). The action is wrapped
    /// to return <c>true</c> so the command is always recorded on the undo stack.
    /// </summary>
    public RelayUndoableCommand(Action doAction, Action undoAction, string description)
        : this(WrapAlwaysSucceeds(doAction), undoAction, description) { }

    /// <summary>
    /// Create a command whose Do function reports success. Return <c>false</c> from
    /// <paramref name="doFunc"/> when the action self-rolled-back (e.g. a refresh threw and the
    /// model was reverted) so <see cref="UndoRedoManager"/> skips recording it (#2231).
    /// </summary>
    public RelayUndoableCommand(Func<bool> doFunc, Action undoAction, string description)
    {
        _do = doFunc ?? throw new ArgumentNullException(nameof(doFunc));
        _undo = undoAction ?? throw new ArgumentNullException(nameof(undoAction));
        Description = description ?? string.Empty;
    }

    private static Func<bool> WrapAlwaysSucceeds(Action doAction)
    {
        if (doAction is null) throw new ArgumentNullException(nameof(doAction));
        return () => { doAction(); return true; };
    }

    public string Description { get; }

    public bool Do() => _do();

    public void Undo() => _undo();
}
