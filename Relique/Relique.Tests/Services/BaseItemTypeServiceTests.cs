using ItemEditor.Services;
using Radoub.Formats.TwoDA;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace ItemEditor.Tests.Services;

/// <summary>
/// Tests for BaseItemTypeService Stacking and ChargesStarting parsing (#1814).
///
/// Stacking column = max stack size (1=single, >1=stackable)
/// ChargesStarting column = initial charges (0=none, >0=charge-based item)
/// </summary>
public class BaseItemTypeServiceTests
{
    private readonly MockGameDataService _mockGameData;

    public BaseItemTypeServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: false);
    }

    private void SetupBaseItems2DA(params (int index, string label, string name, string modelType, string stacking, string chargesStarting)[] items)
    {
        var columns = new[] { "label", "Name", "ModelType", "Stacking", "Description", "ChargesStarting" };
        var twoDA = new TwoDAFile { Columns = new System.Collections.Generic.List<string>(columns) };

        int maxIndex = 0;
        foreach (var item in items)
            if (item.index > maxIndex) maxIndex = item.index;

        for (int i = 0; i <= maxIndex; i++)
        {
            twoDA.Rows.Add(new TwoDARow { Values = new System.Collections.Generic.List<string> { "****", "****", "****", "****", "****", "****" } });
        }

        foreach (var item in items)
        {
            twoDA.Rows[item.index] = new TwoDARow
            {
                Values = new System.Collections.Generic.List<string> { item.label, item.name, item.modelType, item.stacking, "****", item.chargesStarting }
            };
        }

        _mockGameData.With2DA("baseitems", twoDA);
    }

    #region Stacking (max stack size)

    [Fact]
    public void GetBaseItemTypes_StackableItem_StackingIs99()
    {
        SetupBaseItems2DA(
            (25, "BASE_ITEM_ARROW", "****", "0", "99", "****")
        );
        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        var arrow = types.Find(t => t.BaseItemIndex == 25);
        Assert.NotNull(arrow);
        Assert.Equal(99, arrow.Stacking);
        Assert.True(arrow.IsStackable);
    }

    [Fact]
    public void GetBaseItemTypes_SingleItem_StackingIs1()
    {
        SetupBaseItems2DA(
            (1, "BASE_ITEM_LONGSWORD", "****", "0", "1", "****")
        );
        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        var sword = types.Find(t => t.BaseItemIndex == 1);
        Assert.NotNull(sword);
        Assert.Equal(1, sword.Stacking);
        Assert.False(sword.IsStackable);
    }

    [Fact]
    public void GetBaseItemTypes_MissingStacking_DefaultsTo1()
    {
        SetupBaseItems2DA(
            (1, "BASE_ITEM_LONGSWORD", "****", "0", "****", "****")
        );
        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        var sword = types.Find(t => t.BaseItemIndex == 1);
        Assert.NotNull(sword);
        Assert.Equal(1, sword.Stacking);
    }

    #endregion

    #region ChargesStarting

    [Fact]
    public void GetBaseItemTypes_ChargeItem_ChargesStartingParsed()
    {
        SetupBaseItems2DA(
            (43, "BASE_ITEM_MAGICWAND", "****", "0", "1", "50")
        );
        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        var wand = types.Find(t => t.BaseItemIndex == 43);
        Assert.NotNull(wand);
        Assert.Equal(50, wand.ChargesStarting);
        Assert.True(wand.HasCharges);
        Assert.False(wand.IsStackable);
    }

    [Fact]
    public void GetBaseItemTypes_NonChargeItem_ChargesStartingIs0()
    {
        SetupBaseItems2DA(
            (1, "BASE_ITEM_LONGSWORD", "****", "0", "1", "****")
        );
        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        var sword = types.Find(t => t.BaseItemIndex == 1);
        Assert.NotNull(sword);
        Assert.Equal(0, sword.ChargesStarting);
        Assert.False(sword.HasCharges);
    }

    #endregion

    #region IsStackable / HasCharges convenience properties

    [Fact]
    public void IsStackable_TrueWhenStackingGreaterThan1()
    {
        var info = new BaseItemTypeInfo(25, "Arrow", "BASE_ITEM_ARROW", 0, "", 99);
        Assert.True(info.IsStackable);
        Assert.False(info.HasCharges);
    }

    [Fact]
    public void HasCharges_TrueWhenChargesStartingGreaterThan0()
    {
        var info = new BaseItemTypeInfo(43, "Magic Wand", "BASE_ITEM_MAGICWAND", 0, "", 1, 50);
        Assert.False(info.IsStackable);
        Assert.True(info.HasCharges);
    }

    [Fact]
    public void SingleItem_NeitherStackableNorCharges()
    {
        var info = new BaseItemTypeInfo(1, "Longsword", "BASE_ITEM_LONGSWORD", 0);
        Assert.False(info.IsStackable);
        Assert.False(info.HasCharges);
    }

    #endregion
}
