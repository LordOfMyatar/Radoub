using System;

namespace Radoub.UI.Undo;

/// <summary>
/// An <see cref="IUndoableCommand"/> backed by two delegates. Use for one-off mutations that
/// don't warrant a dedicated command class. For simple property edits prefer
/// <see cref="SetFieldCommand{T}"/>, which captures the old value automatically.
/// </summary>
public sealed class RelayUndoableCommand : IUndoableCommand
{
    private readonly Action _do;
    private readonly Action _undo;

    public RelayUndoableCommand(Action doAction, Action undoAction, string description)
    {
        _do = doAction ?? throw new ArgumentNullException(nameof(doAction));
        _undo = undoAction ?? throw new ArgumentNullException(nameof(undoAction));
        Description = description ?? string.Empty;
    }

    public string Description { get; }

    public void Do() => _do();

    public void Undo() => _undo();
}
