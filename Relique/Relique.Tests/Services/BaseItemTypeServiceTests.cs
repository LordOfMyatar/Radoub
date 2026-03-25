using ItemEditor.Services;
using Radoub.Formats.TwoDA;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace ItemEditor.Tests.Services;

/// <summary>
/// Tests for BaseItemTypeService Stacking column parsing (#1814).
/// </summary>
public class BaseItemTypeServiceTests
{
    private readonly MockGameDataService _mockGameData;

    public BaseItemTypeServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: false);
    }

    private void SetupBaseItems2DA(params (int index, string label, string name, string modelType, string stacking)[] items)
    {
        var columns = new[] { "label", "Name", "ModelType", "Stacking", "Description" };
        var twoDA = new TwoDAFile { Columns = new System.Collections.Generic.List<string>(columns) };

        // Pad with empty rows up to the max index
        int maxIndex = 0;
        foreach (var item in items)
            if (item.index > maxIndex) maxIndex = item.index;

        for (int i = 0; i <= maxIndex; i++)
        {
            twoDA.Rows.Add(new TwoDARow { Values = new System.Collections.Generic.List<string> { "****", "****", "****", "****", "****" } });
        }

        foreach (var item in items)
        {
            twoDA.Rows[item.index] = new TwoDARow
            {
                Values = new System.Collections.Generic.List<string> { item.label, item.name, item.modelType, item.stacking, "****" }
            };
        }

        _mockGameData.With2DA("baseitems", twoDA);
    }

    [Fact]
    public void GetBaseItemTypes_ParsesStackingColumn()
    {
        // Arrows are stackable (Stacking=2)
        SetupBaseItems2DA(
            (25, "BASE_ITEM_ARROW", "****", "0", "2")
        );
        // MockGameDataService returns label-based name since Name="****"
        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        var arrow = types.Find(t => t.BaseItemIndex == 25);
        Assert.NotNull(arrow);
        Assert.Equal(2, arrow.Stacking);
    }

    [Fact]
    public void GetBaseItemTypes_StackingSingle_Returns1()
    {
        SetupBaseItems2DA(
            (1, "BASE_ITEM_LONGSWORD", "****", "0", "1")
        );
        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        var sword = types.Find(t => t.BaseItemIndex == 1);
        Assert.NotNull(sword);
        Assert.Equal(1, sword.Stacking);
    }

    [Fact]
    public void GetBaseItemTypes_StackingCharges_Returns3()
    {
        SetupBaseItems2DA(
            (43, "BASE_ITEM_MAGICWAND", "****", "0", "3")
        );
        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        var wand = types.Find(t => t.BaseItemIndex == 43);
        Assert.NotNull(wand);
        Assert.Equal(3, wand.Stacking);
    }

    [Fact]
    public void GetBaseItemTypes_MissingStackingColumn_DefaultsTo1()
    {
        SetupBaseItems2DA(
            (1, "BASE_ITEM_LONGSWORD", "****", "0", "****")
        );
        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        var sword = types.Find(t => t.BaseItemIndex == 1);
        Assert.NotNull(sword);
        Assert.Equal(1, sword.Stacking); // Default: single/not stackable
    }

    [Fact]
    public void BaseItemTypeInfo_IsStackable_TrueForStacking2()
    {
        var info = new BaseItemTypeInfo(25, "Arrow", "BASE_ITEM_ARROW", 0, "", 2);
        Assert.True(info.IsStackable);
        Assert.False(info.HasCharges);
    }

    [Fact]
    public void BaseItemTypeInfo_HasCharges_TrueForStacking3()
    {
        var info = new BaseItemTypeInfo(43, "Magic Wand", "BASE_ITEM_MAGICWAND", 0, "", 3);
        Assert.False(info.IsStackable);
        Assert.True(info.HasCharges);
    }

    [Fact]
    public void BaseItemTypeInfo_Single_NeitherStackableNorCharges()
    {
        var info = new BaseItemTypeInfo(1, "Longsword", "BASE_ITEM_LONGSWORD", 0, "", 1);
        Assert.False(info.IsStackable);
        Assert.False(info.HasCharges);
    }
}
