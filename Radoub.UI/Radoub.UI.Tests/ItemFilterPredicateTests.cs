using Radoub.Formats.Services;
using Radoub.UI.Models;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Coverage for the item palette filter predicate (#2360). The UI-level
/// multi-criteria filter was previously untested — a broken predicate would
/// silently show an incomplete list and the user would assume data is missing.
/// </summary>
public class ItemFilterPredicateTests
{
    private static ItemViewModel Item(
        string name = "Longsword",
        string tag = "sword_tag",
        string resRef = "longsword001",
        int baseItem = 5,
        GameResourceSource source = GameResourceSource.Bif,
        int slotFlags = 0,
        string properties = "")
    {
        return new ItemViewModel
        {
            Name = name,
            Tag = tag,
            ResRef = resRef,
            BaseItem = baseItem,
            Source = source,
            EquipableSlotFlags = slotFlags,
            PropertiesDisplay = properties,
        };
    }

    private static bool Matches(
        ItemViewModel item,
        string search = "",
        string propertySearch = "",
        ItemTypeInfo? type = null,
        SlotFilterInfo? slot = null,
        bool showStandard = true,
        bool showOverride = true,
        bool showHak = true,
        bool showModule = true,
        bool showCreatureItems = true)
        => ItemFilterPredicate.Matches(item, search, propertySearch, type, slot,
            showStandard, showOverride, showHak, showModule, showCreatureItems);

    // ---- Creature/internal item filter (#2411 follow-up) ----

    [Theory]
    [InlineData(69)]  // Creature Bite
    [InlineData(73)]  // Creature Piercing/Bludgeoning
    [InlineData(255)] // Invalid marker
    public void CreatureItem_Hidden_WhenToggleOff(int baseItem)
        => Assert.False(Matches(Item(baseItem: baseItem), showCreatureItems: false));

    [Theory]
    [InlineData(69)]
    [InlineData(255)]
    public void CreatureItem_Shown_WhenToggleOn(int baseItem)
        => Assert.True(Matches(Item(baseItem: baseItem), showCreatureItems: true));

    [Fact]
    public void NormalItem_Unaffected_ByCreatureToggle()
        => Assert.True(Matches(Item(baseItem: 5), showCreatureItems: false));

    // ---- No filters ----

    [Fact]
    public void NoCriteria_MatchesEverything()
        => Assert.True(Matches(Item()));

    // ---- Source filter (#1995: four independent sources) ----

    [Fact]
    public void Standard_Hidden_WhenShowStandardFalse()
        => Assert.False(Matches(Item(source: GameResourceSource.Bif), showStandard: false));

    [Fact]
    public void Standard_Shown_WhenShowStandardTrue()
        => Assert.True(Matches(Item(source: GameResourceSource.Bif), showStandard: true));

    [Fact]
    public void Override_Hidden_WhenShowOverrideFalse()
        => Assert.False(Matches(Item(source: GameResourceSource.Override), showOverride: false));

    [Fact]
    public void Override_Shown_WhenShowOverrideTrue()
        => Assert.True(Matches(Item(source: GameResourceSource.Override), showOverride: true));

    [Fact]
    public void Hak_Hidden_WhenShowHakFalse()
        => Assert.False(Matches(Item(source: GameResourceSource.Hak), showHak: false));

    [Fact]
    public void Hak_Shown_WhenShowHakTrue()
        => Assert.True(Matches(Item(source: GameResourceSource.Hak), showHak: true));

    [Fact]
    public void Module_Hidden_WhenShowModuleFalse()
        => Assert.False(Matches(Item(source: GameResourceSource.Module), showModule: false));

    [Fact]
    public void Module_Shown_WhenShowModuleTrue()
        => Assert.True(Matches(Item(source: GameResourceSource.Module), showModule: true));

    [Fact]
    public void Override_Hidden_DoesNotHideHakOrModule()
    {
        // The granularity the old binary could not express: hide Override but keep HAK + Module.
        Assert.False(Matches(Item(source: GameResourceSource.Override), showOverride: false));
        Assert.True(Matches(Item(source: GameResourceSource.Hak), showOverride: false));
        Assert.True(Matches(Item(source: GameResourceSource.Module), showOverride: false));
    }

    // ---- Type filter ----

    [Fact]
    public void TypeFilter_AllTypes_MatchesAnyBaseItem()
        => Assert.True(Matches(Item(baseItem: 5), type: ItemTypeInfo.AllTypes));

    [Fact]
    public void TypeFilter_MatchingIndex_Passes()
        => Assert.True(Matches(Item(baseItem: 5), type: new ItemTypeInfo(5, "Longsword", "longsword")));

    [Fact]
    public void TypeFilter_NonMatchingIndex_Fails()
        => Assert.False(Matches(Item(baseItem: 5), type: new ItemTypeInfo(7, "Dagger", "dagger")));

    // ---- Slot filter ----

    [Fact]
    public void SlotFilter_AllSlots_MatchesAny()
        => Assert.True(Matches(Item(slotFlags: 0), slot: SlotFilterInfo.AllSlots));

    [Fact]
    public void SlotFilter_NonEquipable_PassesForBackpackItem()
        => Assert.True(Matches(Item(slotFlags: 0), slot: SlotFilterInfo.NonEquipable));

    [Fact]
    public void SlotFilter_NonEquipable_FailsForEquipableItem()
        => Assert.False(Matches(Item(slotFlags: 0x10), slot: SlotFilterInfo.NonEquipable));

    [Fact]
    public void SlotFilter_MatchingFlag_Passes()
        => Assert.True(Matches(Item(slotFlags: 0x18), slot: new SlotFilterInfo(0x08, "Right Hand")));

    [Fact]
    public void SlotFilter_NonOverlappingFlag_Fails()
        => Assert.False(Matches(Item(slotFlags: 0x10), slot: new SlotFilterInfo(0x08, "Right Hand")));

    // ---- Text search (name / tag / resref) ----

    [Fact]
    public void TextSearch_MatchesName_CaseInsensitive()
        => Assert.True(Matches(Item(name: "Flaming Longsword"), search: "flaming"));

    [Fact]
    public void TextSearch_MatchesTag()
        => Assert.True(Matches(Item(tag: "vendor_only"), search: "vendor"));

    [Fact]
    public void TextSearch_MatchesResRef()
        => Assert.True(Matches(Item(resRef: "sword_unique"), search: "unique"));

    [Fact]
    public void TextSearch_NoMatch_Fails()
        => Assert.False(Matches(Item(name: "Longsword", tag: "t", resRef: "r"), search: "nonexistent"));

    // ---- Property search ----

    [Fact]
    public void PropertySearch_Matches_CaseInsensitive()
        => Assert.True(Matches(Item(properties: "Enhancement Bonus +5; Keen"), propertySearch: "keen"));

    [Fact]
    public void PropertySearch_NoMatch_Fails()
        => Assert.False(Matches(Item(properties: "Enhancement Bonus +5"), propertySearch: "vampiric"));

    // ---- Combined criteria (the silent-failure-prone case) ----

    [Fact]
    public void CombinedCriteria_AllSatisfied_Passes()
    {
        var item = Item(name: "Holy Avenger", baseItem: 5, source: GameResourceSource.Hak,
                        slotFlags: 0x08, properties: "Holy Avenger; +5");
        Assert.True(Matches(item,
            search: "holy",
            propertySearch: "avenger",
            type: new ItemTypeInfo(5, "Longsword", "longsword"),
            slot: new SlotFilterInfo(0x08, "Right Hand"),
            showStandard: false,
            showHak: true));
    }

    [Fact]
    public void CombinedCriteria_OneCriterionFails_Fails()
    {
        // Everything matches except the type filter.
        var item = Item(name: "Holy Avenger", baseItem: 5, source: GameResourceSource.Hak,
                        slotFlags: 0x08, properties: "Holy Avenger");
        Assert.False(Matches(item,
            search: "holy",
            type: new ItemTypeInfo(99, "Wrong", "wrong"),
            slot: new SlotFilterInfo(0x08, "Right Hand"),
            showHak: true));
    }
}
