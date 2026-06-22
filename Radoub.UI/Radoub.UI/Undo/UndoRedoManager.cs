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

    /// <summary>Number of commands on the undo stack. Lets a host detect whether an
    /// <see cref="Execute"/> actually recorded (it pushes exactly one entry on success, none on a
    /// self-rolled-back command) without an ambiguous <see cref="CanUndo"/> before/after check.</summary>
    public int UndoCount => _undo.Count;

    /// <summary>Description of the command Undo would revert, or null if none.</summary>
    public string? UndoDescription => _undo.Count > 0 ? _undo.Peek().Description : null;

    /// <summary>Description of the command Redo would reapply, or null if none.</summary>
    public string? RedoDescription => _redo.Count > 0 ? _redo.Peek().Description : null;

    /// <summary>The command Undo would revert next, or null if the stack is empty. Lets a host
    /// inspect the pending command (e.g. to apply a cross-tool lock guard before reverting a write).</summary>
    public IUndoableCommand? PeekUndo => _undo.Count > 0 ? _undo.Peek() : null;

    /// <summary>The command Redo would reapply next, or null if the redo stack is empty.</summary>
    public IUndoableCommand? PeekRedo => _redo.Count > 0 ? _redo.Peek() : null;

    /// <summary>
    /// Run the command, and only if its <see cref="IUndoableCommand.Do"/> reports success
    /// (returns <c>true</c>) push it onto the undo stack and clear the redo stack. A command that
    /// self-rolls-back (returns <c>false</c>) leaves the history untouched — no push, no redo
    /// clear, no <see cref="StateChanged"/> — so a later Undo can't revert a change that the
    /// command already reverted itself (#2231).
    /// </summary>
    public void Execute(IUndoableCommand command)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (!command.Do()) return;
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

    /// <summary>
    /// Reapply the most recently undone command. No-op when the redo stack is empty. If the
    /// reapplied <see cref="IUndoableCommand.Do"/> self-rolls-back (returns <c>false</c>) the
    /// command is dropped — it is not re-pushed onto the undo stack, mirroring
    /// <see cref="Execute"/>'s refuse-to-push contract (#2231).
    /// </summary>
    public void Redo()
    {
        if (_redo.Count == 0) return;
        var command = _redo.Pop();
        if (!command.Do())
        {
            // Do() reverted itself on reapply; drop the command rather than re-push a stale entry.
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
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
