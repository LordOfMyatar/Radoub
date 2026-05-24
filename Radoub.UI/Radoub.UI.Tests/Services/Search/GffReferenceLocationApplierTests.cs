using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Search.Rename;
using Radoub.Formats.Tests.Search.Rename;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

/// <summary>
/// Tests for <see cref="GffReferenceLocationApplier"/> — verifies each branch of the
/// location-string parser correctly updates the GFF tree.
/// </summary>
public class GffReferenceLocationApplierTests
{
    private readonly GffReferenceLocationApplier _applier = new();

    [Fact]
    public void Apply_TopLevelField_UpdatesValue()
    {
        var gff = TestGffBuilder.MakeUtc(conversation: "louis");
        var refRow = MakeRef("Conversation", ResRefScopeTier.TypedGffField, "louis");

        Assert.True(_applier.Apply(gff, refRow, "bob"));
        Assert.Equal("bob", gff.RootStruct.GetField("Conversation")?.Value);
    }

    [Fact]
    public void Apply_NestedListItem_UpdatesValue()
    {
        var gff = TestGffBuilder.MakeGitWithList("Creature List", "TemplateResRef", "louis", "alice");
        var refRow = MakeRef("Creature List > Item 0 > TemplateResRef", ResRefScopeTier.TypedGffField, "louis");

        Assert.True(_applier.Apply(gff, refRow, "bob"));

        var list = gff.RootStruct.GetField("Creature List")?.Value as GffList;
        Assert.NotNull(list);
        Assert.Equal("bob", list!.Elements[0].GetField("TemplateResRef")?.Value);
        Assert.Equal("alice", list.Elements[1].GetField("TemplateResRef")?.Value);  // sibling untouched
    }

    [Fact]
    public void Apply_DlgEntrySound_UpdatesValue()
    {
        var gff = TestGffBuilder.MakeDlgWithSound(entryIndex: 1, sound: "louis_voice");
        var refRow = MakeRef("Entry 1 > Sound", ResRefScopeTier.TypedGffField, "louis_voice");

        Assert.True(_applier.Apply(gff, refRow, "bob_voice"));

        var entries = gff.RootStruct.GetField("EntryList")?.Value as GffList;
        Assert.NotNull(entries);
        Assert.Equal("bob_voice", entries!.Elements[1].GetField("Sound")?.Value);
    }

    [Fact]
    public void Apply_DlgActionParam_UpdatesSubstring()
    {
        var gff = TestGffBuilder.MakeDlgWithActionParam(
            entryIndex: 0, key: "target_resref", value: "the louis here");
        var refRow = new ResRefReference
        {
            FilePath = "/m/x.dlg",
            ResourceType = ResourceTypes.Dlg,
            Field = null,
            Location = "Entry 0 > ActionParams[0] (target_resref)",
            OldValue = "louis",
            NewValue = "bob",
            ScopeTier = ResRefScopeTier.DlgScriptParam,
            MatchOffset = 4,  // offset of "louis" in "the louis here"
            MatchLength = 5
        };

        Assert.True(_applier.Apply(gff, refRow, "bob"));

        var entries = gff.RootStruct.GetField("EntryList")?.Value as GffList;
        Assert.NotNull(entries);
        var actionParams = entries!.Elements[0].GetField("ActionParams")?.Value as GffList;
        Assert.NotNull(actionParams);
        Assert.Equal("the bob here", actionParams!.Elements[0].GetField("Value")?.Value);
    }

    [Fact]
    public void Apply_UnparseableLocation_ReturnsFalse()
    {
        var gff = TestGffBuilder.MakeUtc(conversation: "louis");
        var refRow = MakeRef("garbage > non > sense", ResRefScopeTier.TypedGffField, "louis");

        Assert.False(_applier.Apply(gff, refRow, "bob"));
    }

    [Fact]
    public void Apply_UtmStorePanel_UpdatesValueByPanelName()
    {
        var gff = TestGffBuilder.MakeUtmWithItems("Weapons", "louis_sword", "alice_shield");
        var refRow = MakeRef("Weapons > Item 0 > InventoryRes", ResRefScopeTier.TypedGffField, "louis_sword");

        Assert.True(_applier.Apply(gff, refRow, "bob_sword"));

        var storeList = gff.RootStruct.GetField("StoreList")?.Value as GffList;
        Assert.NotNull(storeList);
        var panel = storeList!.Elements[0];  // first (only) panel
        var items = panel.GetField("ItemList")?.Value as GffList;
        Assert.NotNull(items);
        Assert.Equal("bob_sword", items!.Elements[0].GetField("InventoryRes")?.Value);
    }

    private static ResRefReference MakeRef(string location, ResRefScopeTier tier, string oldValue) => new()
    {
        FilePath = "/m/test",
        ResourceType = ResourceTypes.Utc,
        Field = null,
        Location = location,
        OldValue = oldValue,
        NewValue = string.Empty,
        ScopeTier = tier
    };
}
