using System.IO;
using System.Linq;
using Radoub.Formats.Fac;
using PlaceableEditor.Services;

namespace PlaceableEditor.Tests.Services;

/// <summary>
/// FactionService (#2354) loads the module's repute.fac into (Id, Name) pairs for the Behavior
/// panel's Faction combo, falling back to the five standard NWN factions when no module / no
/// repute.fac. No hardcoded faction data when a module file is present — mirrors Quartermaster's
/// AppearanceService.GetAllFactions.
/// </summary>
public class FactionServiceTests
{
    [Fact]
    public void Load_NullModuleDir_ReturnsStandardFallback()
    {
        var factions = FactionService.Load(null);

        Assert.Equal(5, factions.Count);
        Assert.Equal((ushort)0, factions[0].Id);
        Assert.Equal("PC", factions[0].Name);
        Assert.Equal("Hostile", factions[1].Name);
    }

    [Fact]
    public void Load_ModuleDirWithoutReputeFac_ReturnsStandardFallback()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var factions = FactionService.Load(dir);
            Assert.Equal(5, factions.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_ReputeFacPresent_ReturnsFactionsInIndexOrder()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var fac = new FacFile();
            fac.FactionList.Add(new Faction { FactionName = "PC" });
            fac.FactionList.Add(new Faction { FactionName = "Hostile" });
            fac.FactionList.Add(new Faction { FactionName = "Townsfolk" });
            fac.FactionList.Add(new Faction { FactionName = "Bandits" });
            FacWriter.Write(fac, Path.Combine(dir, "repute.fac"));

            var factions = FactionService.Load(dir);

            Assert.Equal(4, factions.Count);
            Assert.Equal((ushort)2, factions[2].Id);
            Assert.Equal("Townsfolk", factions[2].Name);
            Assert.Equal("Bandits", factions[3].Name);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_EmptyFactionName_FallsBackToIndexedLabel()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var fac = new FacFile();
            fac.FactionList.Add(new Faction { FactionName = "PC" });
            fac.FactionList.Add(new Faction { FactionName = "" });
            FacWriter.Write(fac, Path.Combine(dir, "repute.fac"));

            var factions = FactionService.Load(dir);

            Assert.Equal("Faction 1", factions[1].Name);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
