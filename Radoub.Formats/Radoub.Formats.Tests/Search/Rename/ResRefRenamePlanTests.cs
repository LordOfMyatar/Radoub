using Radoub.Formats.Common;
using Radoub.Formats.Search.Rename;
using Xunit;

namespace Radoub.Formats.Tests.Search.Rename;

public class ResRefRenamePlanTests
{
    private static ResRefRenamePlan MakePlan() => new()
    {
        OldName = "louis",
        NewName = "bob",
        ResourceType = ResourceTypes.Dlg,
        Validation = ResRefValidationResult.Ok("bob"),
        SourceFilePath = "/m/louis.dlg",
        TargetFilePath = "/m/bob.dlg"
    };

    [Fact]
    public void NewPlan_HasNoReferences()
    {
        var plan = MakePlan();
        Assert.Empty(plan.References);
        Assert.Empty(plan.SelectedReferences);
    }

    [Fact]
    public void IsSelected_DefaultsTrue()
    {
        var plan = MakePlan();
        Assert.True(plan.IsSelected);
    }

    [Fact]
    public void SelectedReferences_FiltersUntickedRows()
    {
        var plan = MakePlan();
        plan.References.Add(MakeRef(selected: true));
        plan.References.Add(MakeRef(selected: false));
        plan.References.Add(MakeRef(selected: true));

        Assert.Equal(2, plan.SelectedReferences.Count());
    }

    private static ResRefReference MakeRef(bool selected) => new()
    {
        FilePath = "/m/file.utc",
        ResourceType = ResourceTypes.Utc,
        Location = "Conversation",
        OldValue = "louis",
        NewValue = "bob",
        ScopeTier = ResRefScopeTier.TypedGffField,
        IsSelected = selected
    };
}
