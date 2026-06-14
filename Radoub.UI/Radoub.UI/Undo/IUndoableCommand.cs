namespace Radoub.UI.Undo;

/// <summary>
/// A reversible action. Editors push these to an <see cref="UndoRedoManager"/> instead of
/// mutating the model directly, so every change can be undone and redone.
/// Foundation for the cross-tool undo epic (#2231); first consumer is Reliquary (#2295).
/// </summary>
public interface IUndoableCommand
{
    /// <summary>
    /// Apply the change. Called once on Execute and again on each Redo.
    /// Returns <c>true</c> when the change was applied and should be recorded on the undo stack;
    /// <c>false</c> when the command self-rolled-back (e.g. a UI refresh threw and the model was
    /// reverted) and must NOT be recorded — recording it would let a later Undo revert a change
    /// that never happened. See <see cref="UndoRedoManager.Execute"/> / <see cref="UndoRedoManager.Redo"/>.
    /// </summary>
    bool Do();

    /// <summary>Revert the change applied by <see cref="Do"/>.</summary>
    void Undo();

    /// <summary>Human-readable label for menu text ("Undo change HP").</summary>
    string Description { get; }
}
