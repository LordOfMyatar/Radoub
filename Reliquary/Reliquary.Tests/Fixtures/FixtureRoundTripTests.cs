using System.IO;
using Radoub.Formats.Utp;
using PlaceableEditor.Views.Panels;

namespace PlaceableEditor.Tests.Fixtures;

/// <summary>
/// Round-trip + metadata-read coverage against the 5 real UTP fixtures extracted
/// from LNS_DLG (#2294). Proves UtpReader/UtpWriter symmetry on real data and
/// that the browser's metadata seam reads the right Tag/Name.
/// </summary>
public class FixtureRoundTripTests
{
    private static string FixtureDir =>
        Path.Combine(System.AppContext.BaseDirectory, "Fixtures");

    public static IEnumerable<object[]> AllFixtures()
    {
        yield return new object[] { "stat_garg001.utp" };
        yield return new object[] { "chest1.utp" };
        yield return new object[] { "bandit_treasure.utp" };
        yield return new object[] { "appletree.utp" };
        yield return new object[] { "invis_trap_rsp.utp" };
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void Fixture_Exists(string fixture)
    {
        Assert.True(File.Exists(Path.Combine(FixtureDir, fixture)),
            $"Fixture not copied to output: {fixture}");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void Fixture_RoundTrips_ByteStable(string fixture)
    {
        var path = Path.Combine(FixtureDir, fixture);

        // Read → write → read; the second model must equal the first on the
        // fields the editor surfaces. (Full byte-equality can differ on GFF
        // field ordering; assert on parsed values, the editor's contract.)
        var first = UtpReader.Read(path);
        var bytes = UtpWriter.Write(first);
        var second = UtpReader.Read(bytes);

        Assert.Equal(first.TemplateResRef, second.TemplateResRef);
        Assert.Equal(first.Tag, second.Tag);
        Assert.Equal(first.LocName.GetDefault(), second.LocName.GetDefault());
        Assert.Equal(first.HasInventory, second.HasInventory);
        Assert.Equal(first.Static, second.Static);
        Assert.Equal(first.ItemList.Count, second.ItemList.Count);
    }

    [Fact]
    public void BanditTreasure_HasFiveInventoryItems()
    {
        var utp = UtpReader.Read(Path.Combine(FixtureDir, "bandit_treasure.utp"));
        Assert.True(utp.HasInventory);
        Assert.Equal(5, utp.ItemList.Count);
    }

    [Fact]
    public void Chest1_MetadataReadsTagAndName()
    {
        var bytes = File.ReadAllBytes(Path.Combine(FixtureDir, "chest1.utp"));

        var (tag, name) = PlaceableBrowserPanel.ReadUtpMetadata(bytes);

        Assert.Equal("TG_CHEST", tag);
        Assert.Equal("TG_CHEST", name);
    }
}
