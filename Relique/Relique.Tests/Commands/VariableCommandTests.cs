using System.Collections.ObjectModel;
using System.Linq;
using ItemEditor.Commands;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;
using Xunit;

namespace ItemEditor.Tests.Commands;

/// <summary>
/// Tests for Relique's variable undo/redo commands (Sprint 1 of epic #2231). Relique derives the
/// model VarTable from the UI collection at save time, so the commands operate on the
/// ObservableCollection only.
/// </summary>
public class VariableCommandTests
{
    private static VariableViewModel Var(string name) => new() { Name = name };

    private static ObservableCollection<VariableViewModel> Collection(params string[] names)
    {
        var c = new ObservableCollection<VariableViewModel>();
        foreach (var n in names) c.Add(Var(n));
        return c;
    }

    [Fact]
    public void AddVariable_DoUndoRedo_RoundTrips()
    {
        var vars = Collection("a");
        var vm = Var("b");
        var cmd = new AddVariableCommand(vars, vm);

        Assert.True(cmd.Do());                 // [a, b]
        Assert.Equal(new[] { "a", "b" }, vars.Select(v => v.Name));
        cmd.Undo();                            // [a]
        Assert.Equal(new[] { "a" }, vars.Select(v => v.Name));
        Assert.True(cmd.Do());                 // redo → [a, b]
        Assert.Equal(new[] { "a", "b" }, vars.Select(v => v.Name));
    }

    [Fact]
    public void RemoveVariables_Undo_RestoresAtOriginalIndices()
    {
        var vars = Collection("a", "b", "c", "d");
        var toRemove = new[] { vars[1], vars[3] }; // b, d
        var cmd = new RemoveVariablesCommand(vars, toRemove);

        Assert.True(cmd.Do());                  // [a, c]
        Assert.Equal(new[] { "a", "c" }, vars.Select(v => v.Name));
        cmd.Undo();                             // [a, b, c, d]
        Assert.Equal(new[] { "a", "b", "c", "d" }, vars.Select(v => v.Name));
    }

    [Fact]
    public void RemoveVariables_DoUndoRedo_RoundTrips()
    {
        var vars = Collection("a", "b", "c");
        var cmd = new RemoveVariablesCommand(vars, new[] { vars[1] });

        cmd.Do();    // [a, c]
        cmd.Undo();  // [a, b, c]
        Assert.True(cmd.Do()); // redo → [a, c]
        Assert.Equal(new[] { "a", "c" }, vars.Select(v => v.Name));
    }

    [Fact]
    public void RemoveVariables_NonePresent_ReportsFalse()
    {
        var vars = Collection("a");
        var cmd = new RemoveVariablesCommand(vars, new[] { Var("ghost") });

        Assert.False(cmd.Do()); // not present → don't record
        Assert.Single(vars);
    }

    [Fact]
    public void Manager_RecordsAndUndoes_VariableAdd()
    {
        var mgr = new UndoRedoManager();
        var vars = Collection("a");

        mgr.Execute(new AddVariableCommand(vars, Var("b")));
        Assert.True(mgr.CanUndo);
        Assert.Equal(2, vars.Count);

        mgr.Undo();
        Assert.Single(vars);
    }
}
