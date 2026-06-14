using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.UI.Undo;

namespace ItemEditor.Views;

/// <summary>
/// Undo/redo wiring for Relique (Sprint 1 of epic #2231). Mirrors Reliquary's pattern: a
/// per-document <see cref="UndoRedoManager"/>, menu enablement refreshed on StateChanged, and
/// Ctrl+Z/Y dispatched from the window key handler with a TextBox-focus guard (see
/// <see cref="MainWindow.OnWindowKeyDown"/>). Property/variable mutations are routed through
/// <see cref="IUndoableCommand"/> instances; scalar TextBox edits keep Avalonia's native undo.
/// </summary>
public partial class MainWindow
{
    private readonly UndoRedoManager _undo = new();
    private bool _editorWired;

    /// <summary>Connect the undo manager to menu refresh + dirty marking once (called from construction).</summary>
    private void WireEditor()
    {
        if (_editorWired) return;
        _editorWired = true;

        _undo.StateChanged += (_, _) => RefreshUndoMenu();
        // Command-based edits flow through the undo manager; binding-based scalar edits flow through
        // the VM. Mark dirty on undo state changes too so the title bar reflects undo/redo.
        _undo.StateChanged += (_, _) => MarkDirty();
        RefreshUndoMenu();
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e) => _undo.Undo();

    private void OnRedoClick(object? sender, RoutedEventArgs e) => _undo.Redo();

    /// <summary>
    /// Sync the Edit-menu Undo/Redo items to the manager state — enablement plus the
    /// "_Undo {description}" hint. InputGesture on a MenuItem only renders hint text in Avalonia;
    /// the actual accelerator is dispatched from <see cref="MainWindow.OnWindowKeyDown"/>.
    /// </summary>
    private void RefreshUndoMenu()
    {
        var undoItem = this.FindControl<MenuItem>("UndoMenuItem");
        if (undoItem != null)
        {
            undoItem.IsEnabled = _undo.CanUndo;
            undoItem.Header = _undo.CanUndo ? $"_Undo {_undo.UndoDescription}" : "_Undo";
        }

        var redoItem = this.FindControl<MenuItem>("RedoMenuItem");
        if (redoItem != null)
        {
            redoItem.IsEnabled = _undo.CanRedo;
            redoItem.Header = _undo.CanRedo ? $"_Redo {_undo.RedoDescription}" : "_Redo";
        }
    }
}
