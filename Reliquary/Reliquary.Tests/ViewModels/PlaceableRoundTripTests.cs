using System.IO;
using Radoub.Formats.Utp;
using PlaceableEditor.ViewModels;

namespace PlaceableEditor.Tests.ViewModels;

/// <summary>
/// ViewModel-layer round-trip: load real UTP → wrap in PlaceableViewModel → write back →
/// reread, asserting editor-surfaced fields survive the facade (design §6.4). Complements
/// the format-layer FixtureRoundTripTests which proves UtpReader/UtpWriter symmetry directly.
/// </summary>
public class PlaceableRoundTripTests
{
    private static string FixtureDir => Path.Combine(System.AppContext.BaseDirectory, "Fixtures");

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
    public void Load_NoMutation_PreservesFields(string fixture)
    {
        var original = UtpReader.Read(Path.Combine(FixtureDir, fixture));

        var vm = new PlaceableViewModel(UtpReader.Read(Path.Combine(FixtureDir, fixture)));
        var written = UtpWriter.Write(vm.WriteToUtp());
        var reread = UtpReader.Read(written);

        Assert.Equal(original.TemplateResRef, reread.TemplateResRef);
        Assert.Equal(original.Tag, reread.Tag);
        Assert.Equal(original.LocName.GetDefault(), reread.LocName.GetDefault());
        Assert.Equal(original.HP, reread.HP);
        Assert.Equal(original.HasInventory, reread.HasInventory);
        Assert.Equal(original.Static, reread.Static);
        Assert.Equal(original.ItemList.Count, reread.ItemList.Count);
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void Load_MutatePaletteID_Preserved(string fixture)
    {
        // #2416: setting the palette category survives a save/reload round-trip.
        var vm = new PlaceableViewModel(UtpReader.Read(Path.Combine(FixtureDir, fixture)));
        vm.PaletteID = 9;
        var reread = UtpReader.Read(UtpWriter.Write(vm.WriteToUtp()));

        Assert.Equal((byte)9, reread.PaletteID);
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void Load_MutateTag_OnlyTagChanges(string fixture)
    {
        var original = UtpReader.Read(Path.Combine(FixtureDir, fixture));

        var vm = new PlaceableViewModel(UtpReader.Read(Path.Combine(FixtureDir, fixture)));
        vm.Tag = "MUTATED_TAG";
        var reread = UtpReader.Read(UtpWriter.Write(vm.WriteToUtp()));

        Assert.Equal("MUTATED_TAG", reread.Tag);
        // Everything else stable.
        Assert.Equal(original.TemplateResRef, reread.TemplateResRef);
        Assert.Equal(original.HP, reread.HP);
        Assert.Equal(original.LocName.GetDefault(), reread.LocName.GetDefault());
    }

    [Fact]
    public void Load_MutateScript_PersistsThroughWrite()
    {
        var vm = new PlaceableViewModel(UtpReader.Read(Path.Combine(FixtureDir, "chest1.utp")));

        vm.Scripts.First(s => s.EventName == "OnOpen").ResRef = "my_onopen";
        var reread = UtpReader.Read(UtpWriter.Write(vm.WriteToUtp()));

        Assert.Equal("my_onopen", reread.OnOpen);
    }
}
