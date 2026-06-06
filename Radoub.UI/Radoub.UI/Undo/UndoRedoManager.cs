using System;
using System.Collections.Generic;

namespace Radoub.UI.Undo;

/// <summary>
/// Two-stack undo/redo history. Editors call <see cref="Execute"/> for every model mutation;
/// <see cref="Undo"/>/<see cref="Redo"/> walk the history. Executing a new command after an undo
/// clears the redo stack (linear history, no branching).
///
/// Per-document: call <see cref="Clear"/> on file open so each document starts with a fresh
/// history. Foundation for the cross-tool undo epic (#2231); first consumer is Reliquary (#2295).
/// </summary>
public sealed class UndoRedoManager
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    /// <summary>Raised after any state change (execute, undo, redo, clear) so hosts can refresh menu enablement.</summary>
    public event EventHandler? StateChanged;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Description of the command Undo would revert, or null if none.</summary>
    public string? UndoDescription => _undo.Count > 0 ? _undo.Peek().Description : null;

    /// <summary>Description of the command Redo would reapply, or null if none.</summary>
    public string? RedoDescription => _redo.Count > 0 ? _redo.Peek().Description : null;

    /// <summary>Run the command, push it onto the undo stack, and clear the redo stack.</summary>
    public void Execute(IUndoableCommand command)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        command.Do();
        _undo.Push(command);
        _redo.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Revert the most recent command. No-op when the undo stack is empty.</summary>
    public void Undo()
    {
        if (_undo.Count == 0) return;
        var command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reapply the most recently undone command. No-op when the redo stack is empty.</summary>
    public void Redo()
    {
        if (_redo.Count == 0) return;
        var command = _redo.Pop();
        command.Do();
        _undo.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drop all history (call on file open/close).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
