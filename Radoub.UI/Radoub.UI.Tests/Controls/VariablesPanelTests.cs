using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Radoub.Formats.Gff;
using Radoub.UI.Controls;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests.Controls;

/// <summary>
/// Tests for the shared <see cref="VariablesPanel"/> control. The panel is undo-agnostic:
/// it raises Add/Replace/Delete events the host turns into undoable commands (#2293).
/// </summary>
public class VariablesPanelTests
{
    [AvaloniaFact]
    public void Construct_DoesNotThrow()
    {
        var panel = new VariablesPanel();
        Assert.NotNull(panel.Variables);
    }

    [AvaloniaFact]
    public void Variables_BindsObservableCollection()
    {
        var panel = new VariablesPanel();
        var vars = new ObservableCollection<VariableViewModel>
        {
            VariableViewModel.FromVariable(Variable.CreateInt("nA", 1))
        };

        panel.Variables = vars;

        Assert.Same(vars, panel.Variables);
        Assert.Single(panel.Variables);
    }

    [AvaloniaFact]
    public void RequestAdd_RaisesEvent_PanelDoesNotMutateCollection()
    {
        var panel = new VariablesPanel { Variables = new ObservableCollection<VariableViewModel>() };
        VariableAddRequestedEventArgs? captured = null;
        panel.AddRequested += (_, e) => captured = e;

        panel.RequestAdd();

        // Panel raises the request; the HOST owns the mutation (undo-agnostic).
        Assert.NotNull(captured);
        Assert.Empty(panel.Variables);
    }

    [AvaloniaFact]
    public void RequestDelete_RaisesEventWithSelectedVariable()
    {
        var target = VariableViewModel.FromVariable(Variable.CreateString("sX", "y"));
        var panel = new VariablesPanel
        {
            Variables = new ObservableCollection<VariableViewModel> { target },
            SelectedVariable = target
        };
        VariableViewModel? deleted = null;
        panel.DeleteRequested += (_, e) => deleted = e.Variable;

        panel.RequestDelete();

        Assert.Same(target, deleted);
        Assert.Single(panel.Variables); // host removes, not the panel
    }

    [AvaloniaFact]
    public void RequestDelete_NoSelection_DoesNotRaise()
    {
        var panel = new VariablesPanel { Variables = new ObservableCollection<VariableViewModel>() };
        var raised = false;
        panel.DeleteRequested += (_, _) => raised = true;

        panel.RequestDelete();

        Assert.False(raised);
    }

    [AvaloniaFact]
    public void DuplicateName_FlagsBothVariables()
    {
        var a = new VariableViewModel { Name = "dup", Type = VariableType.Int };
        var b = new VariableViewModel { Name = "dup", Type = VariableType.Int };
        var panel = new VariablesPanel { Variables = new ObservableCollection<VariableViewModel> { a, b } };

        panel.RevalidateNames();

        Assert.True(a.HasError);
        Assert.True(b.HasError);
    }

    [AvaloniaFact]
    public void UniqueNames_NoErrors()
    {
        var a = new VariableViewModel { Name = "alpha", Type = VariableType.Int };
        var b = new VariableViewModel { Name = "beta", Type = VariableType.Int };
        var panel = new VariablesPanel { Variables = new ObservableCollection<VariableViewModel> { a, b } };

        panel.RevalidateNames();

        Assert.False(a.HasError);
        Assert.False(b.HasError);
    }

    [AvaloniaFact]
    public void InvalidName_FlaggedOnRevalidate()
    {
        var bad = new VariableViewModel { Name = "has space", Type = VariableType.Int };
        var panel = new VariablesPanel { Variables = new ObservableCollection<VariableViewModel> { bad } };

        panel.RevalidateNames();

        Assert.True(bad.HasError);
    }

    // --- Self-validation: panel validates on edit without host plumbing (#2293 follow-up) ---

    [AvaloniaFact]
    public void EditingItemName_AutoValidates_NoManualRevalidateCall()
    {
        var a = new VariableViewModel { Name = "ok", Type = VariableType.Int, ValueText = "1" };
        var b = new VariableViewModel { Name = "unique", Type = VariableType.Int, ValueText = "2" };
        var panel = new VariablesPanel { Variables = new ObservableCollection<VariableViewModel> { a, b } };

        b.Name = "ok"; // create a duplicate by editing — panel must catch it on its own

        Assert.True(a.HasError);
        Assert.True(b.HasError);
    }

    [AvaloniaFact]
    public void BadValue_FlaggedByPanel()
    {
        var v = new VariableViewModel { Name = "n", Type = VariableType.Int, ValueText = "1" };
        var panel = new VariablesPanel { Variables = new ObservableCollection<VariableViewModel> { v } };

        v.ValueText = "abc"; // bad int

        Assert.True(v.HasError);
        Assert.Contains("whole number", v.ErrorMessage);
    }

    [AvaloniaFact]
    public void VariablesChanged_RaisedOnUserEdit_NotOnAssign()
    {
        var panel = new VariablesPanel();
        var raised = 0;
        panel.VariablesChanged += (_, _) => raised++;

        // Assigning the collection (host populate) must NOT count as a user edit.
        var v = new VariableViewModel { Name = "n", Type = VariableType.Int, ValueText = "1" };
        panel.Variables = new ObservableCollection<VariableViewModel> { v };
        Assert.Equal(0, raised);

        // Editing an item IS a user edit.
        v.ValueText = "2";
        Assert.True(raised >= 1);
    }

    [AvaloniaFact]
    public void HasValidationErrors_TrueWhenValueBad()
    {
        var v = new VariableViewModel { Name = "n", Type = VariableType.Int, ValueText = "x" };
        var panel = new VariablesPanel { Variables = new ObservableCollection<VariableViewModel> { v } };

        panel.RevalidateNames();

        Assert.True(panel.HasValidationErrors);
    }

    // --- GridMaxHeight bounds the inner DataGrid so it scrolls (#2293 follow-up) ---

    [AvaloniaFact]
    public void GridMaxHeight_BoundsTheInnerGrid()
    {
        var panel = new VariablesPanel { GridMaxHeight = 240 };
        var window = new Window { Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var grid = panel.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
        Assert.NotNull(grid);
        Assert.Equal(240, grid!.MaxHeight);
    }
}
