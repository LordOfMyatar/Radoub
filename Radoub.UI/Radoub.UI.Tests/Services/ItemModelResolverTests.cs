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
    public void Resolve_Armor_RobeHidesBodyParts_DropsSuppressedAndKeepsRobe()
    {
        // #2582: a robe item must not double-render the body parts it covers on the mannequin.
        // parts_robe.2da row 186 hides chest + bicepl; the resolver must drop those but keep the
        // robe itself and any part the robe does NOT hide (forer here).
        var game = BuildGameData();
        game.Set2DAValue("parts_robe", 186, "HIDECHEST", "1");
        game.Set2DAValue("parts_robe", 186, "HIDEBICEPL", "1");
        game.Set2DAValue("parts_robe", 186, "HIDEFORER", "0");
        game.SetResource("pmh0_robe186", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("pmh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("pmh0_bicepl010", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("pmh0_forer003", ResourceTypes.Mdl, new byte[] { 1 });

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile
        {
            BaseItem = 3,
            ArmorParts = new()
            {
                ["Robe"] = 186,
                ["Torso"] = 5,
                ["LBicep"] = 10,
                ["RFArm"] = 3,
            },
        };

        var result = resolver.Resolve(uti);

        Assert.Contains("pmh0_robe186", result.MdlResRefs);   // robe kept
        Assert.Contains("pmh0_forer003", result.MdlResRefs);  // not hidden by this robe
        Assert.DoesNotContain("pmh0_chest005", result.MdlResRefs);   // hidden (HIDECHEST)
        Assert.DoesNotContain("pmh0_bicepl010", result.MdlResRefs);  // hidden (HIDEBICEPL)
    }

    [Fact]
    public void Resolve_Armor_CloakOnlyRobe_KeepsBody()
    {
        // robe116 (cloak) hides only the shoulders — the mannequin body parts must all render.
        var game = BuildGameData();
        game.Set2DAValue("parts_robe", 116, "HIDESHOL", "1");
        game.Set2DAValue("parts_robe", 116, "HIDECHEST", "0");
        game.SetResource("pmh0_robe116", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("pmh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("pmh0_shol010", ResourceTypes.Mdl, new byte[] { 1 });

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile
        {
            BaseItem = 3,
            ArmorParts = new() { ["Robe"] = 116, ["Torso"] = 5, ["LShoul"] = 10 },
        };

        var result = resolver.Resolve(uti);

        Assert.Contains("pmh0_robe116", result.MdlResRefs);
        Assert.Contains("pmh0_chest005", result.MdlResRefs);   // body kept
        Assert.DoesNotContain("pmh0_shol010", result.MdlResRefs);  // shoulder hidden
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
    public void Resolve_Armor_FemaleMannequinPrefix_ReturnsFemaleBodyPartResRefs()
    {
        var game = BuildGameData();
        // Female mannequin prefix is pfh0 (playable female human, phenotype 0)
        game.SetResource("pfh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("pfh0_pelvis002", ResourceTypes.Mdl, new byte[] { 1 });

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game, "pfh0");
        var uti = new UtiFile
        {
            BaseItem = 3,
            ArmorParts = new()
            {
                ["Torso"] = 5,
                ["Pelvis"] = 2,
            },
        };

        var result = resolver.Resolve(uti);

        Assert.True(result.HasModel);
        Assert.Contains("pfh0_chest005", result.MdlResRefs);
        Assert.Contains("pfh0_pelvis002", result.MdlResRefs);
    }

    [Fact]
    public void ArmorMannequinPrefix_SetAtRuntime_AffectsNextResolve()
    {
        var game = BuildGameData();
        game.SetResource("pmh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });
        game.SetResource("pfh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });

        var resolver = new ItemModelResolver(BuildBaseItemService(game), game);
        var uti = new UtiFile { BaseItem = 3, ArmorParts = new() { ["Torso"] = 5 } };

        Assert.Contains("pmh0_chest005", resolver.Resolve(uti).MdlResRefs);

        resolver.ArmorMannequinPrefix = "pfh0";

        Assert.Contains("pfh0_chest005", resolver.Resolve(uti).MdlResRefs);
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
