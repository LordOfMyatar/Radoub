using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
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
}
