using System.Reflection;
using Radoub.Formats.Fac;
using Radoub.Formats.Gff;
using RadoubLauncher.Services;
using RadoubLauncher.ViewModels;

namespace Trebuchet.Tests;

/// <summary>
/// Tests that FactionEditorViewModel.RemoveFaction() triggers area .git reindexing (#1317).
/// Uses reflection to set internal state since the ViewModel normally loads from RadoubSettings.
/// </summary>
public class FactionEditorReindexTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FactionEditorViewModel _viewModel;

    public FactionEditorReindexTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FactionReindexTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _viewModel = new FactionEditorViewModel();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDirectory, true); } catch { }
    }

    /// <summary>
    /// Sets up the ViewModel with a FacFile and working directory path via reflection.
    /// </summary>
    private void SetupViewModel(FacFile facFile)
    {
        var facFileField = typeof(FactionEditorViewModel).GetField("_facFile",
            BindingFlags.NonPublic | BindingFlags.Instance);
        facFileField!.SetValue(_viewModel, facFile);

        var workDirField = typeof(FactionEditorViewModel).GetField("_workingDirectoryPath",
            BindingFlags.NonPublic | BindingFlags.Instance);
        workDirField!.SetValue(_viewModel, _testDirectory);

        // Build the view models from the FacFile
        var buildVm = typeof(FactionEditorViewModel).GetMethod("BuildViewModels",
            BindingFlags.NonPublic | BindingFlags.Instance);
        buildVm!.Invoke(_viewModel, null);

        var buildMatrix = typeof(FactionEditorViewModel).GetMethod("BuildMatrix",
            BindingFlags.NonPublic | BindingFlags.Instance);
        buildMatrix!.Invoke(_viewModel, null);
    }

    /// <summary>
    /// Creates a FacFile with 5 defaults + custom factions.
    /// </summary>
    private FacFile CreateFacFile(params (string name, uint parent)[] customFactions)
    {
        var fac = FacReader.CreateDefault();

        foreach (var (name, parent) in customFactions)
        {
            fac.FactionList.Add(new Faction
            {
                FactionName = name,
                FactionGlobal = 0,
                FactionParentID = parent
            });

            int newIndex = fac.FactionList.Count - 1;
            for (int i = 0; i < fac.FactionList.Count; i++)
            {
                if (i == newIndex) continue;
                fac.RepList.Add(new Reputation { FactionID1 = (uint)newIndex, FactionID2 = (uint)i, FactionRep = 50 });
                fac.RepList.Add(new Reputation { FactionID1 = (uint)i, FactionID2 = (uint)newIndex, FactionRep = 50 });
            }
        }

        return fac;
    }

    private string CreateGitFile(string name, uint[] creatureFactionIds, uint[]? encounterFactions = null)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Creature FactionID is WORD (ushort) in real .git files
        var creatureList = new GffList();
        foreach (var fid in creatureFactionIds)
        {
            var creature = new GffStruct { Type = 4 };
            GffFieldBuilder.AddWordField(creature, "FactionID", (ushort)fid);
            GffFieldBuilder.AddCResRefField(creature, "TemplateResRef", "test_cr");
            creatureList.Elements.Add(creature);
        }
        creatureList.Count = (uint)creatureList.Elements.Count;
        GffFieldBuilder.AddListField(root, "Creature List", creatureList);

        // Encounter Faction is DWORD (uint) in real .git files
        var encounterList = new GffList();
        if (encounterFactions != null)
        {
            foreach (var fid in encounterFactions)
            {
                var encounter = new GffStruct { Type = 7 };
                GffFieldBuilder.AddDwordField(encounter, "Faction", fid);
                encounterList.Elements.Add(encounter);
            }
        }
        encounterList.Count = (uint)encounterList.Elements.Count;
        GffFieldBuilder.AddListField(root, "Encounter List", encounterList);

        var gff = new GffFile { FileType = "GIT ", FileVersion = "V3.2", RootStruct = root };
        var filePath = Path.Combine(_testDirectory, name + ".git");
        GffWriter.Write(gff, filePath);
        return filePath;
    }

    private uint[] ReadCreatureFactionIds(string filePath)
    {
        var gff = GffReader.Read(filePath);
        var list = gff.RootStruct.GetField("Creature List")?.Value as GffList;
        return list?.Elements.Select(s => s.GetFieldValue<uint>("FactionID", 0)).ToArray()
               ?? Array.Empty<uint>();
    }

    private uint[] ReadEncounterFactions(string filePath)
    {
        var gff = GffReader.Read(filePath);
        var list = gff.RootStruct.GetField("Encounter List")?.Value as GffList;
        return list?.Elements.Select(s => s.GetFieldValue<uint>("Faction", 0)).ToArray()
               ?? Array.Empty<uint>();
    }

    [Fact]
    public void RemoveFaction_ReindexesCreatureInGitFile()
    {
        // Custom faction at index 5, parent = Merchant (3)
        var fac = CreateFacFile(("Bandits", 3));
        SetupViewModel(fac);

        // Place creature with FactionID=5 (Bandits)
        var gitPath = CreateGitFile("area001", new uint[] { 5 });

        // Remove faction 5 (Bandits)
        var banditsFaction = _viewModel.Factions.First(f => f.Name == "Bandits");
        _viewModel.RemoveFaction(banditsFaction);

        // Creature should be reassigned to parent (Merchant = 3)
        var factionIds = ReadCreatureFactionIds(gitPath);
        Assert.Equal(3u, factionIds[0]);
    }

    [Fact]
    public void RemoveFaction_ReindexesEncounterInGitFile()
    {
        var fac = CreateFacFile(("Guards", 4)); // Parent = Defender (4)
        SetupViewModel(fac);

        var gitPath = CreateGitFile("area001", Array.Empty<uint>(), new uint[] { 5 });

        var guardsFaction = _viewModel.Factions.First(f => f.Name == "Guards");
        _viewModel.RemoveFaction(guardsFaction);

        var factions = ReadEncounterFactions(gitPath);
        Assert.Equal(4u, factions[0]);
    }

    [Fact]
    public void RemoveFaction_DecrementsHigherFactionIds()
    {
        // Two custom factions: index 5 (Bandits) and index 6 (Guards)
        var fac = CreateFacFile(("Bandits", 3), ("Guards", 4));
        SetupViewModel(fac);

        // Creature on faction 6 (Guards)
        var gitPath = CreateGitFile("area001", new uint[] { 6 });

        // Delete faction 5 (Bandits) — Guards should shift from 6 to 5
        var banditsFaction = _viewModel.Factions.First(f => f.Name == "Bandits");
        _viewModel.RemoveFaction(banditsFaction);

        var factionIds = ReadCreatureFactionIds(gitPath);
        Assert.Equal(5u, factionIds[0]);
    }

    [Fact]
    public void RemoveFaction_NoGitFiles_StatusShowsRemovalOnly()
    {
        var fac = CreateFacFile(("Bandits", 3));
        SetupViewModel(fac);

        // No .git files in directory
        var banditsFaction = _viewModel.Factions.First(f => f.Name == "Bandits");
        _viewModel.RemoveFaction(banditsFaction);

        Assert.Contains("Removed faction: Bandits", _viewModel.StatusText);
        Assert.DoesNotContain("reindexed", _viewModel.StatusText);
    }

    [Fact]
    public void RemoveFaction_WithReindexedCreatures_StatusShowsCount()
    {
        var fac = CreateFacFile(("Bandits", 3));
        SetupViewModel(fac);

        CreateGitFile("area001", new uint[] { 5, 5 }); // Two creatures on Bandits

        var banditsFaction = _viewModel.Factions.First(f => f.Name == "Bandits");
        _viewModel.RemoveFaction(banditsFaction);

        Assert.Contains("reindexed", _viewModel.StatusText);
        Assert.Contains("2", _viewModel.StatusText);
    }

    [Fact]
    public void RemoveFaction_UnaffectedCreatures_Untouched()
    {
        var fac = CreateFacFile(("Bandits", 3));
        SetupViewModel(fac);

        // Creature on Defender (4) — below deleted index 5
        var gitPath = CreateGitFile("area001", new uint[] { 4 });

        var banditsFaction = _viewModel.Factions.First(f => f.Name == "Bandits");
        _viewModel.RemoveFaction(banditsFaction);

        var factionIds = ReadCreatureFactionIds(gitPath);
        Assert.Equal(4u, factionIds[0]); // Unchanged
    }
}
