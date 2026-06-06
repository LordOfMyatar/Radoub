namespace Radoub.UI.Undo;

/// <summary>
/// A reversible action. Editors push these to an <see cref="UndoRedoManager"/> instead of
/// mutating the model directly, so every change can be undone and redone.
/// Foundation for the cross-tool undo epic (#2231); first consumer is Reliquary (#2295).
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Apply the change. Called once on Execute and again on each Redo.</summary>
    void Do();

    /// <summary>Revert the change applied by <see cref="Do"/>.</summary>
    void Undo();

    /// <summary>Human-readable label for menu text ("Undo change HP").</summary>
    string Description { get; }
}
