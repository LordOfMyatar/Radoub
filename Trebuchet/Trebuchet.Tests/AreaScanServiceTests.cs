using Radoub.Formats.Gff;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for AreaScanService — scans area .git files for creature/encounter
/// faction references and supports reindexing on faction delete.
/// </summary>
public class AreaScanServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public AreaScanServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"AreaScanTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDirectory, true); } catch { }
    }

    #region Helper: Build .git GFF files

    /// <summary>
    /// Creates a minimal .git GFF file with specified creature FactionIDs and encounter Factions.
    /// Creature FactionID is WORD (ushort) matching real .git files.
    /// Encounter Faction is DWORD (uint) matching real .git files.
    /// </summary>
    private string CreateGitFile(string name, uint[] creatureFactionIds, uint[]? encounterFactions = null)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Build Creature List — FactionID is WORD in real .git files
        var creatureList = new GffList();
        foreach (var factionId in creatureFactionIds)
        {
            var creature = new GffStruct { Type = 4 };
            GffFieldBuilder.AddWordField(creature, "FactionID", (ushort)factionId);
            GffFieldBuilder.AddCResRefField(creature, "TemplateResRef", "test_creature");
            creatureList.Elements.Add(creature);
        }
        creatureList.Count = (uint)creatureList.Elements.Count;
        GffFieldBuilder.AddListField(root, "Creature List", creatureList);

        // Build Encounter List — Faction is DWORD in real .git files
        var encounterList = new GffList();
        if (encounterFactions != null)
        {
            foreach (var faction in encounterFactions)
            {
                var encounter = new GffStruct { Type = 7 };
                GffFieldBuilder.AddDwordField(encounter, "Faction", faction);
                GffFieldBuilder.AddCResRefField(encounter, "TemplateResRef", "test_encounter");
                encounterList.Elements.Add(encounter);
            }
        }
        encounterList.Count = (uint)encounterList.Elements.Count;
        GffFieldBuilder.AddListField(root, "Encounter List", encounterList);

        var gff = new GffFile
        {
            FileType = "GIT ",
            FileVersion = "V3.2",
            RootStruct = root
        };

        var filePath = Path.Combine(_testDirectory, name + ".git");
        GffWriter.Write(gff, filePath);
        return filePath;
    }

    /// <summary>
    /// Creates a minimal .utc GFF file with specified FactionID.
    /// FactionID is WORD (ushort) in UTC files.
    /// </summary>
    private string CreateUtcFile(string name, uint factionId)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddWordField(root, "FactionID", (ushort)factionId);
        GffFieldBuilder.AddCResRefField(root, "TemplateResRef", name);

        var gff = new GffFile
        {
            FileType = "UTC ",
            FileVersion = "V3.2",
            RootStruct = root
        };

        var filePath = Path.Combine(_testDirectory, name + ".utc");
        GffWriter.Write(gff, filePath);
        return filePath;
    }

    private uint ReadUtcFactionId(string filePath)
    {
        var gff = GffReader.Read(filePath);
        return gff.RootStruct.GetFieldValue<uint>("FactionID", 0);
    }

    /// <summary>
    /// Reads a .git file and extracts creature FactionIDs.
    /// </summary>
    private uint[] ReadCreatureFactionIds(string filePath)
    {
        var gff = GffReader.Read(filePath);
        var creatureListField = gff.RootStruct.GetField("Creature List");
        if (creatureListField?.Value is not GffList list) return Array.Empty<uint>();

        return list.Elements
            .Select(s => s.GetFieldValue<uint>("FactionID", 0))
            .ToArray();
    }

    /// <summary>
    /// Reads a .git file and extracts encounter Factions.
    /// </summary>
    private uint[] ReadEncounterFactions(string filePath)
    {
        var gff = GffReader.Read(filePath);
        var encounterListField = gff.RootStruct.GetField("Encounter List");
        if (encounterListField?.Value is not GffList list) return Array.Empty<uint>();

        return list.Elements
            .Select(s => s.GetFieldValue<uint>("Faction", 0))
            .ToArray();
    }

    #endregion

    #region FindGitFiles

    [Fact]
    public void FindGitFiles_EmptyDirectory_ReturnsEmpty()
    {
        var files = AreaScanService.FindGitFiles(_testDirectory);
        Assert.Empty(files);
    }

    [Fact]
    public void FindGitFiles_WithGitFiles_ReturnsAll()
    {
        CreateGitFile("area001", new uint[] { 0, 1 });
        CreateGitFile("area002", new uint[] { 2 });

        var files = AreaScanService.FindGitFiles(_testDirectory);
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void FindGitFiles_IgnoresNonGitFiles()
    {
        CreateGitFile("area001", new uint[] { 0 });
        File.WriteAllText(Path.Combine(_testDirectory, "module.ifo"), "not a git file");

        var files = AreaScanService.FindGitFiles(_testDirectory);
        Assert.Single(files);
    }

    [Fact]
    public void FindGitFiles_NullDirectory_ReturnsEmpty()
    {
        var files = AreaScanService.FindGitFiles(null);
        Assert.Empty(files);
    }

    #endregion

    #region ScanFactionReferences

    [Fact]
    public void ScanFactionReferences_FindsCreatureFactionIds()
    {
        var filePath = CreateGitFile("area001", new uint[] { 0, 3, 5 });

        var refs = AreaScanService.ScanFactionReferences(_testDirectory);

        Assert.Single(refs);
        Assert.Contains(0u, refs[0].CreatureFactionIds);
        Assert.Contains(3u, refs[0].CreatureFactionIds);
        Assert.Contains(5u, refs[0].CreatureFactionIds);
    }

    [Fact]
    public void ScanFactionReferences_FindsEncounterFactions()
    {
        var filePath = CreateGitFile("area001", new uint[] { 0 }, new uint[] { 1, 2 });

        var refs = AreaScanService.ScanFactionReferences(_testDirectory);

        Assert.Single(refs);
        Assert.Contains(1u, refs[0].EncounterFactionIds);
        Assert.Contains(2u, refs[0].EncounterFactionIds);
    }

    [Fact]
    public void ScanFactionReferences_MultipleAreas()
    {
        CreateGitFile("area001", new uint[] { 5 });
        CreateGitFile("area002", new uint[] { 3 }, new uint[] { 5 });

        var refs = AreaScanService.ScanFactionReferences(_testDirectory);

        Assert.Equal(2, refs.Count);
    }

    [Fact]
    public void ScanFactionReferences_NoFactionRefs_StillReturnsEntry()
    {
        CreateGitFile("area001", Array.Empty<uint>());

        var refs = AreaScanService.ScanFactionReferences(_testDirectory);

        Assert.Single(refs);
        Assert.Empty(refs[0].CreatureFactionIds);
        Assert.Empty(refs[0].EncounterFactionIds);
    }

    #endregion

    #region HasFactionReferences

    [Fact]
    public void HasFactionReferences_FactionUsedByCreature_ReturnsTrue()
    {
        CreateGitFile("area001", new uint[] { 0, 5, 2 });

        Assert.True(AreaScanService.HasFactionReferences(_testDirectory, 5));
    }

    [Fact]
    public void HasFactionReferences_FactionUsedByEncounter_ReturnsTrue()
    {
        CreateGitFile("area001", new uint[] { 0 }, new uint[] { 5 });

        Assert.True(AreaScanService.HasFactionReferences(_testDirectory, 5));
    }

    [Fact]
    public void HasFactionReferences_FactionNotUsed_ReturnsFalse()
    {
        CreateGitFile("area001", new uint[] { 0, 1, 2 });

        Assert.False(AreaScanService.HasFactionReferences(_testDirectory, 5));
    }

    [Fact]
    public void HasFactionReferences_EmptyDirectory_ReturnsFalse()
    {
        Assert.False(AreaScanService.HasFactionReferences(_testDirectory, 5));
    }

    #endregion

    #region ReindexFactions

    [Fact]
    public void ReindexFactions_CreatureWithDeletedFaction_ReassignedToParent()
    {
        // Creature has FactionID=5, deleting faction 5 with parent 3
        var filePath = CreateGitFile("area001", new uint[] { 5 });

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(1, result.FilesModified);
        Assert.Equal(1, result.CreaturesReindexed);

        var factionIds = ReadCreatureFactionIds(filePath);
        Assert.Equal(3u, factionIds[0]);
    }

    [Fact]
    public void ReindexFactions_CreatureAboveDeletedIndex_Decremented()
    {
        // Creature has FactionID=7, deleting faction 5 — should become 6
        var filePath = CreateGitFile("area001", new uint[] { 7 });

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(1, result.FilesModified);
        var factionIds = ReadCreatureFactionIds(filePath);
        Assert.Equal(6u, factionIds[0]);
    }

    [Fact]
    public void ReindexFactions_CreatureBelowDeletedIndex_Unchanged()
    {
        // Creature has FactionID=2, deleting faction 5 — should stay 2
        var filePath = CreateGitFile("area001", new uint[] { 2 });

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(0, result.FilesModified);
        var factionIds = ReadCreatureFactionIds(filePath);
        Assert.Equal(2u, factionIds[0]);
    }

    [Fact]
    public void ReindexFactions_EncounterWithDeletedFaction_ReassignedToParent()
    {
        var filePath = CreateGitFile("area001", Array.Empty<uint>(), new uint[] { 5 });

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(1, result.FilesModified);
        Assert.Equal(1, result.EncountersReindexed);

        var factions = ReadEncounterFactions(filePath);
        Assert.Equal(3u, factions[0]);
    }

    [Fact]
    public void ReindexFactions_EncounterAboveDeletedIndex_Decremented()
    {
        var filePath = CreateGitFile("area001", Array.Empty<uint>(), new uint[] { 8 });

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        var factions = ReadEncounterFactions(filePath);
        Assert.Equal(7u, factions[0]);
    }

    [Fact]
    public void ReindexFactions_MixedScenario_AllCorrect()
    {
        // area001: creature on faction 5 (deleted), creature on faction 7 (above), creature on faction 2 (below)
        // area001 also has encounter on faction 5
        var filePath = CreateGitFile("area001",
            new uint[] { 5, 7, 2 },
            new uint[] { 5 });

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(1, result.FilesModified);
        Assert.Equal(1, result.CreaturesReindexed);
        Assert.Equal(1, result.EncountersReindexed);

        var creatureIds = ReadCreatureFactionIds(filePath);
        Assert.Equal(3u, creatureIds[0]); // was 5, reassigned to parent 3
        Assert.Equal(6u, creatureIds[1]); // was 7, decremented to 6
        Assert.Equal(2u, creatureIds[2]); // was 2, unchanged

        var encounterIds = ReadEncounterFactions(filePath);
        Assert.Equal(3u, encounterIds[0]); // was 5, reassigned to parent 3
    }

    [Fact]
    public void ReindexFactions_MultipleAreas_OnlyModifiedAreasWritten()
    {
        // area001 has creature on deleted faction — should be modified
        var file1 = CreateGitFile("area001", new uint[] { 5 });
        // area002 has no affected creatures — should NOT be modified
        var file2 = CreateGitFile("area002", new uint[] { 2 });

        var time1Before = File.GetLastWriteTimeUtc(file2);
        // Small delay to ensure timestamp difference
        System.Threading.Thread.Sleep(50);

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(1, result.FilesModified);
        Assert.Equal(2, result.FilesScanned);
    }

    [Fact]
    public void ReindexFactions_NoAffectedCreatures_NoFilesModified()
    {
        CreateGitFile("area001", new uint[] { 0, 1, 2 });
        CreateGitFile("area002", new uint[] { 3 });

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(0, result.FilesModified);
        Assert.Equal(0, result.CreaturesReindexed);
    }

    [Fact]
    public void ReindexFactions_EmptyDirectory_ZeroResult()
    {
        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(0, result.FilesScanned);
        Assert.Equal(0, result.FilesModified);
    }

    [Fact]
    public void ReindexFactions_ParentAlsoAboveDeleted_ParentDecrementedToo()
    {
        // Deleting faction 5 with parent 7.
        // Creature on faction 5 should go to parent 7, but parent 7 becomes 6 after reindex.
        // So creature should end up at 6.
        var filePath = CreateGitFile("area001", new uint[] { 5 });

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 7);

        var factionIds = ReadCreatureFactionIds(filePath);
        // Parent 7 is above deleted 5, so the new parent index after deletion is 6
        Assert.Equal(6u, factionIds[0]);
    }

    [Fact]
    public void ReindexFactions_ParentIsNoParent_FallsBackToCommoner()
    {
        // Deleting faction 5 with no parent (0xFFFFFFFF)
        var filePath = CreateGitFile("area001", new uint[] { 5 });

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 0xFFFFFFFF);

        var factionIds = ReadCreatureFactionIds(filePath);
        // 0xFFFFFFFF means "no parent" — fall back to Commoner (2), a safe neutral faction.
        // PC (0) is not viable. Hostile (1) causes unintended aggression.
        Assert.Equal(2u, factionIds[0]);
    }

    [Fact]
    public void ReindexFactions_PreservesWordType_ForCreatureFactionId()
    {
        // Creature FactionID is WORD (ushort) in .git files — verify type preserved after reindex
        var filePath = CreateGitFile("area001", new uint[] { 5 });

        AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        var gff = GffReader.Read(filePath);
        var creatureList = gff.RootStruct.GetField("Creature List")?.Value as GffList;
        var field = creatureList!.Elements[0].GetField("FactionID");
        Assert.Equal(GffField.WORD, field!.Type);
        Assert.IsType<ushort>(field.Value);
        Assert.Equal((ushort)3, field.Value);
    }

    #endregion

    #region UTC Blueprint Reindexing

    [Fact]
    public void ReindexFactions_UtcWithDeletedFaction_ReassignedToParent()
    {
        var utcPath = CreateUtcFile("bandit001", 5);

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(1, result.BlueprintsReindexed);
        Assert.Equal(3u, ReadUtcFactionId(utcPath));
    }

    [Fact]
    public void ReindexFactions_UtcAboveDeletedIndex_Decremented()
    {
        var utcPath = CreateUtcFile("guard001", 7);

        AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(6u, ReadUtcFactionId(utcPath));
    }

    [Fact]
    public void ReindexFactions_UtcBelowDeletedIndex_Unchanged()
    {
        var utcPath = CreateUtcFile("merchant001", 2);

        AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(2u, ReadUtcFactionId(utcPath));
    }

    [Fact]
    public void ReindexFactions_MixedGitAndUtc_AllReindexed()
    {
        var gitPath = CreateGitFile("area001", new uint[] { 5 });
        var utcPath = CreateUtcFile("bandit001", 5);

        var result = AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(1, result.CreaturesReindexed);
        Assert.Equal(1, result.BlueprintsReindexed);
        Assert.Equal(2, result.FilesModified);
        Assert.Equal(3u, ReadCreatureFactionIds(gitPath)[0]);
        Assert.Equal(3u, ReadUtcFactionId(utcPath));
    }

    [Fact]
    public void ReindexFactions_UtcPreservesWordType()
    {
        var utcPath = CreateUtcFile("bandit001", 5);

        AreaScanService.ReindexFactions(_testDirectory, deletedIndex: 5, parentFactionId: 3);

        var gff = GffReader.Read(utcPath);
        var field = gff.RootStruct.GetField("FactionID");
        Assert.Equal(GffField.WORD, field!.Type);
        Assert.IsType<ushort>(field.Value);
    }

    #endregion
}
