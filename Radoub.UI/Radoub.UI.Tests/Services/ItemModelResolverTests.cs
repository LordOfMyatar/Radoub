using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.TwoDA;
using Radoub.Formats.Uti;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

public class ItemModelResolverTests
{
    private static MockGameDataService BuildGameData()
    {
        var game = new MockGameDataService(includeSampleData: false);

        var baseItems = new TwoDAFile();
        baseItems.Columns.AddRange(new[] { "Label", "ItemClass", "ModelType", "DefaultModel" });

        // Row 0: Simple weapon (longsword)
        baseItems.Rows.Add(new TwoDARow { Label = "0", Values = new() { "BASE_ITEM_LONGSWORD", "w_swrd", "0", "w_swrd_001" } });
        // Row 1: Layered (cloak)
        baseItems.Rows.Add(new TwoDARow { Label = "1", Values = new() { "BASE_ITEM_CLOAK", "cloak", "1", "cloak_001" } });
        // Row 2: Composite weapon (twobladed sword)
        baseItems.Rows.Add(new TwoDARow { Label = "2", Values = new() { "BASE_ITEM_TWOBLADEDSWORD", "wdbsw", "2", "wdbsw_b_001" } });
        // Row 3: Armor
        baseItems.Rows.Add(new TwoDARow { Label = "3", Values = new() { "BASE_ITEM_ARMOR", "armor", "3", "****" } });
        // Row 4: Unknown ModelType
        baseItems.Rows.Add(new TwoDARow { Label = "4", Values = new() { "BASE_ITEM_BOGUS", "bogus", "9", "****" } });

        game.With2DA("baseitems", baseItems);
        return game;
    }

    private static BaseItemTypeService BuildBaseItemService(MockGameDataService game) => new(game);

    [Fact]
    public void Resolve_SimpleWeapon_ReturnsSingleResRefAndNoColors()
    {
        var game = BuildGameData();
        game.SetResource("w_swrd_005", ResourceTypes.Mdl, new byte[] { 1 });

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 0, ModelPart1 = 5 };

        var result = resolver.Resolve(uti);

        Assert.True(result.HasModel);
        Assert.Single(result.MdlResRefs);
        Assert.Equal("w_swrd_005", result.MdlResRefs[0]);
        Assert.False(result.HasArmorParts);
        Assert.False(result.HasColorFields);
    }

    [Fact]
    public void Resolve_LayeredItem_ReturnsSingleResRefAndColorFields()
    {
        var game = BuildGameData();
        game.SetResource("cloak_003", ResourceTypes.Mdl, new byte[] { 1 });

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 1, ModelPart1 = 3 };

        var result = resolver.Resolve(uti);

        Assert.True(result.HasModel);
        Assert.Single(result.MdlResRefs);
        Assert.Equal("cloak_003", result.MdlResRefs[0]);
        Assert.False(result.HasArmorParts);
        Assert.True(result.HasColorFields);
    }

    [Fact]
    public void Resolve_CompositeWeapon_ReturnsThreeResRefsBmt()
    {
        var game = BuildGameData();
        game.SetResource("wdbsw_b_011", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("wdbsw_m_021", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("wdbsw_t_031", ResourceTypes.Mdl, new byte[] { 1 });

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 2, ModelPart1 = 11, ModelPart2 = 21, ModelPart3 = 31 };

        var result = resolver.Resolve(uti);

        Assert.True(result.HasModel);
        Assert.Equal(3, result.MdlResRefs.Count);
        Assert.Contains("wdbsw_b_011", result.MdlResRefs);
        Assert.Contains("wdbsw_m_021", result.MdlResRefs);
        Assert.Contains("wdbsw_t_031", result.MdlResRefs);
        Assert.False(result.HasArmorParts);
        Assert.False(result.HasColorFields);
    }

    [Fact]
    public void Resolve_Armor_ReturnsBodyPartResRefsWithMannequinPrefix()
    {
        var game = BuildGameData();
        // Default mannequin prefix is pmh0 (playable male human, phenotype 0)
        game.SetResource("pmh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("pmh0_pelvis002", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("pmh0_belt001", ResourceTypes.Mdl, new byte[] { 1 });

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile
        {
            BaseItem = 3,
            ArmorParts = new()
            {
                ["Torso"] = 5,
                ["Pelvis"] = 2,
                ["Belt"] = 1,
            },
        };

        var result = resolver.Resolve(uti);

        Assert.True(result.HasModel);
        Assert.True(result.HasArmorParts);
        Assert.True(result.HasColorFields);
        Assert.Contains("pmh0_chest005", result.MdlResRefs);
        Assert.Contains("pmh0_pelvis002", result.MdlResRefs);
        Assert.Contains("pmh0_belt001", result.MdlResRefs);
    }

    [Fact]
    public void Resolve_Armor_PartialPartsExist_DropsMissingAndKeepsRest()
    {
        var game = BuildGameData();
        game.SetResource("pmh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });
        // pelvis and belt MDLs intentionally not registered

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile
        {
            BaseItem = 3,
            ArmorParts = new()
            {
                ["Torso"] = 5,
                ["Pelvis"] = 2,
                ["Belt"] = 1,
            },
        };

        var result = resolver.Resolve(uti);

        Assert.True(result.HasModel);
        Assert.Single(result.MdlResRefs);
        Assert.Equal("pmh0_chest005", result.MdlResRefs[0]);
    }

    [Fact]
    public void Resolve_Armor_AllPartsMissing_ReturnsHasModelFalse()
    {
        var game = BuildGameData();
        // No resources registered

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile
        {
            BaseItem = 3,
            ArmorParts = new() { ["Torso"] = 5, ["Pelvis"] = 2 },
        };

        var result = resolver.Resolve(uti);

        Assert.False(result.HasModel);
        Assert.Empty(result.MdlResRefs);
    }

    [Fact]
    public void Resolve_SimpleWeapon_PartZero_TreatedAsNoModel()
    {
        var game = BuildGameData();

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 0, ModelPart1 = 0 };

        var result = resolver.Resolve(uti);

        Assert.False(result.HasModel);
        Assert.Empty(result.MdlResRefs);
    }

    [Fact]
    public void Resolve_SimpleWeapon_MdlMissing_ReturnsHasModelFalse()
    {
        var game = BuildGameData();
        // w_swrd_005 not registered as a resource

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 0, ModelPart1 = 5 };

        var result = resolver.Resolve(uti);

        Assert.False(result.HasModel);
        Assert.Empty(result.MdlResRefs);
    }

    [Fact]
    public void Resolve_BaseItemOutOfRange_ReturnsHasModelFalse()
    {
        var game = BuildGameData();
        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 999, ModelPart1 = 1 };

        var result = resolver.Resolve(uti);

        Assert.False(result.HasModel);
        Assert.Empty(result.MdlResRefs);
        Assert.False(result.HasArmorParts);
        Assert.False(result.HasColorFields);
    }

    [Fact]
    public void Resolve_UnknownModelType_ReturnsHasModelFalse()
    {
        var game = BuildGameData();
        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 4, ModelPart1 = 1 };

        var result = resolver.Resolve(uti);

        Assert.False(result.HasModel);
    }

    [Fact]
    public void Resolve_Composite_OneOfThreePartsMissing_DropsMissingAndKeepsRest()
    {
        var game = BuildGameData();
        game.SetResource("wdbsw_b_011", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("wdbsw_t_031", ResourceTypes.Mdl, new byte[] { 1 });
        // middle part absent

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 2, ModelPart1 = 11, ModelPart2 = 21, ModelPart3 = 31 };

        var result = resolver.Resolve(uti);

        Assert.True(result.HasModel);
        Assert.Equal(2, result.MdlResRefs.Count);
        Assert.DoesNotContain("wdbsw_m_021", result.MdlResRefs);
    }

    [Fact]
    public void Resolve_NullUti_Throws()
    {
        var game = BuildGameData();
        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);

        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }

    [Fact]
    public void Resolve_EmptyArmorPartsDictionary_ReturnsHasModelFalse()
    {
        var game = BuildGameData();
        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 3, ArmorParts = new() };

        var result = resolver.Resolve(uti);

        Assert.False(result.HasModel);
        Assert.Empty(result.MdlResRefs);
    }
}
