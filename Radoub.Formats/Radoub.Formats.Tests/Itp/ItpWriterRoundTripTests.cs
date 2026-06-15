using Radoub.Formats.Itp;
using Xunit;

namespace Radoub.Formats.Tests.Itp;

/// <summary>
/// Round-trip tests for ItpWriter: build an ItpFile object graph, write to GFF bytes,
/// read back with ItpReader, and assert structural equality. Exercises every node/field
/// type including the #2280 deep-nested category case.
/// </summary>
public class ItpWriterRoundTripTests
{
    [Fact]
    public void Write_Read_RoundTripsAllNodeAndFieldTypes()
    {
        var itpOriginal = BuildSampleItp();

        var bytes1 = ItpWriter.Write(itpOriginal);
        var itpReread = ItpReader.Read(bytes1);

        Assert.NotNull(itpReread);
        AssertTreesEqual(itpOriginal, itpReread!);
    }

    // MAIN -> [ branch (STRREF + NAME) -> (
    //   category id10 (TYPE + DELETE_ME) -> blueprint (STRREF + NAME + RESREF + CR + FACTION),
    //   category id20 -> nested category id21 -> blueprint (NAME + RESREF)   <- #2280 deep case
    // ) ]
    private static ItpFile BuildSampleItp()
    {
        var creatureBlueprint = new PaletteBlueprintNode
        {
            StrRef = 1234,
            Name = "Goblin Grunt",
            ResRef = "goblin_grunt01",
            ChallengeRating = 1.5f,
            Faction = "Hostile"
        };

        var category10 = new PaletteCategoryNode
        {
            Id = 10,
            DisplayType = PaletteDisplayType.DisplayCustom,
            DeleteMe = "deleteme-marker"
        };
        category10.Blueprints.Add(creatureBlueprint);

        var deepBlueprint = new PaletteBlueprintNode
        {
            Name = "KAF Armor 001",
            ResRef = "kaf_armor001"
        };

        var nestedCategory21 = new PaletteCategoryNode { Id = 21 };
        nestedCategory21.Blueprints.Add(deepBlueprint);

        var category20 = new PaletteCategoryNode { Id = 20 };
        category20.Children.Add(nestedCategory21);

        var branch = new PaletteBranchNode
        {
            StrRef = 5678,
            Name = "Faction Specific"
        };
        branch.Children.Add(category10);
        branch.Children.Add(category20);

        var itp = new ItpFile
        {
            FileType = "ITP ",
            FileVersion = "V3.2",
            ResType = 2027,
            NextUseableId = 99
        };
        itp.MainNodes.Add(branch);
        return itp;
    }

    private static void AssertTreesEqual(ItpFile expected, ItpFile actual)
    {
        Assert.Equal(expected.FileType, actual.FileType);
        Assert.Equal(expected.FileVersion, actual.FileVersion);
        Assert.Equal(expected.ResType, actual.ResType);
        Assert.Equal(expected.NextUseableId, actual.NextUseableId);

        AssertNodeListEqual(expected.MainNodes, actual.MainNodes);
    }

    private static void AssertNodeListEqual(List<PaletteNode> expected, List<PaletteNode> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertNodeEqual(expected[i], actual[i]);
        }
    }

    private static void AssertNodeEqual(PaletteNode expected, PaletteNode actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.StrRef, actual.StrRef);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.DeleteMe, actual.DeleteMe);
        Assert.Equal(expected.DisplayType, actual.DisplayType);

        switch (expected)
        {
            case PaletteBlueprintNode bp:
                var actualBp = (PaletteBlueprintNode)actual;
                Assert.Equal(bp.ResRef, actualBp.ResRef);
                Assert.Equal(bp.ChallengeRating, actualBp.ChallengeRating);
                Assert.Equal(bp.Faction, actualBp.Faction);
                break;

            case PaletteCategoryNode cat:
                var actualCat = (PaletteCategoryNode)actual;
                Assert.Equal(cat.Id, actualCat.Id);
                AssertBlueprintBucketEqual(cat.Blueprints, actualCat.Blueprints);
                AssertNodeListEqual(cat.Children, actualCat.Children);
                break;

            case PaletteBranchNode br:
                var actualBr = (PaletteBranchNode)actual;
                AssertNodeListEqual(br.Children, actualBr.Children);
                break;
        }
    }

    // Blueprints are emitted as one contiguous bucket, order preserved -> positional compare.
    private static void AssertBlueprintBucketEqual(
        List<PaletteBlueprintNode> expected,
        List<PaletteBlueprintNode> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertNodeEqual(expected[i], actual[i]);
        }
    }
}
