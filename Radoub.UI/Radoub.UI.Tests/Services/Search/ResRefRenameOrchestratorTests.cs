using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Search.Rename;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class ResRefRenameOrchestratorTests : IDisposable
{
    private readonly string _root;

    public ResRefRenameOrchestratorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"orch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static ResRefRenamePlan MakeRenamePlan(string moduleDir, string oldName, string newName) => new()
    {
        OldName = oldName,
        NewName = newName,
        ResourceType = ResourceTypes.Utc,
        Validation = ResRefValidationResult.Ok(newName),
        SourceFilePath = Path.Combine(moduleDir, $"{oldName}.utc"),
        TargetFilePath = Path.Combine(moduleDir, $"{newName}.utc")
    };

    /// <summary>Read a GIT's first creature TemplateResRef value (test helper).</summary>
    private static string? GetGitCreatureTemplateResRef(string gitPath)
    {
        var git = GffReader.Read(File.ReadAllBytes(gitPath));
        var list = git.RootStruct.GetField("Creature List")?.Value as GffList;
        if (list == null || list.Elements.Count == 0) return null;
        return list.Elements[0].GetField("TemplateResRef")?.Value as string;
    }

    [Fact]
    public async Task ExecuteAsync_SimpleRename_RenamesFileAndUpdatesReferences()
    {
        var moduleDir = TestModuleFixture.CreateMinimalModule(_root);
        var backupDir = Path.Combine(_root, ".backups");

        // Plan: rename louis_roumain.utc → louis.utc
        var plan = MakeRenamePlan(moduleDir, "louis_roumain", "louis");
        plan.References.Add(new ResRefReference
        {
            FilePath = Path.Combine(moduleDir, "area01.git"),
            ResourceType = ResourceTypes.Git,
            Field = null,
            Location = "Creature List > Item 0 > TemplateResRef",
            OldValue = "louis_roumain",
            NewValue = "louis",
            ScopeTier = ResRefScopeTier.TypedGffField,
            IsSelected = true
        });

        var orchestrator = new ResRefRenameOrchestrator(new BackupService(backupDir));
        var result = await orchestrator.ExecuteAsync(new[] { plan }, moduleName: "test");

        Assert.True(result.Success, $"Execute failed: {result.Error}");
        Assert.Equal(1, result.RenamedFiles);
        Assert.Equal(1, result.ReferencesUpdated);

        // File renamed on disk
        Assert.False(File.Exists(Path.Combine(moduleDir, "louis_roumain.utc")));
        Assert.True(File.Exists(Path.Combine(moduleDir, "louis.utc")));

        // GIT reference updated
        Assert.Equal("louis", GetGitCreatureTemplateResRef(Path.Combine(moduleDir, "area01.git")));
    }

    [Fact]
    public async Task ExecuteAsync_TopLevelScalarReference_UpdatesField()
    {
        var moduleDir = TestModuleFixture.CreateMinimalModule(_root);
        var backupDir = Path.Combine(_root, ".backups");

        // louis_dlg.dlg → renamed to "louis_v2"
        // The reference is: louis_roumain.utc's Conversation field points at "louis_dlg"
        var plan = new ResRefRenamePlan
        {
            OldName = "louis_dlg",
            NewName = "louis_v2",
            ResourceType = ResourceTypes.Dlg,
            Validation = ResRefValidationResult.Ok("louis_v2"),
            SourceFilePath = Path.Combine(moduleDir, "louis_dlg.dlg"),
            TargetFilePath = Path.Combine(moduleDir, "louis_v2.dlg")
        };
        plan.References.Add(new ResRefReference
        {
            FilePath = Path.Combine(moduleDir, "louis_roumain.utc"),
            ResourceType = ResourceTypes.Utc,
            Field = new FieldDefinition
            {
                Name = "Conversation",
                GffPath = "Conversation",
                FieldType = SearchFieldType.ResRef,
                Category = SearchFieldCategory.Metadata
            },
            Location = "Conversation",
            OldValue = "louis_dlg",
            NewValue = "louis_v2",
            ScopeTier = ResRefScopeTier.TypedGffField,
            IsSelected = true
        });

        var orchestrator = new ResRefRenameOrchestrator(new BackupService(backupDir));
        var result = await orchestrator.ExecuteAsync(new[] { plan }, "test");

        Assert.True(result.Success, $"Execute failed: {result.Error}");

        var utc = GffReader.Read(File.ReadAllBytes(Path.Combine(moduleDir, "louis_roumain.utc")));
        Assert.Equal("louis_v2", utc.RootStruct.GetField("Conversation")?.Value as string);
    }

    [Fact]
    public async Task ExecuteAsync_NssReference_RewritesScriptText()
    {
        var moduleDir = TestModuleFixture.CreateMinimalModule(_root);
        var backupDir = Path.Combine(_root, ".backups");

        var nssPath = Path.Combine(moduleDir, "script1.nss");
        var source = File.ReadAllText(nssPath);
        var quotedIdx = source.IndexOf("\"louis_roumain\"", StringComparison.Ordinal);
        Assert.True(quotedIdx >= 0);  // sanity: fixture contains the quoted token

        var plan = MakeRenamePlan(moduleDir, "louis_roumain", "louis");
        plan.References.Add(new ResRefReference
        {
            FilePath = nssPath,
            ResourceType = ResourceTypes.Nss,
            Field = null,
            Location = "Line 2 (quoted)",
            OldValue = "louis_roumain",
            NewValue = "louis",
            ScopeTier = ResRefScopeTier.NssQuotedString,
            MatchOffset = quotedIdx + 1,  // inside the quotes
            MatchLength = "louis_roumain".Length,
            IsSelected = true
        });

        var orchestrator = new ResRefRenameOrchestrator(new BackupService(backupDir));
        var result = await orchestrator.ExecuteAsync(new[] { plan }, "test");

        Assert.True(result.Success, $"Execute failed: {result.Error}");

        var nssText = File.ReadAllText(nssPath);
        Assert.DoesNotContain("louis_roumain", nssText);
        Assert.Contains("\"louis\"", nssText);
    }

    [Fact]
    public async Task ExecuteAsync_UntickedReference_NotApplied()
    {
        var moduleDir = TestModuleFixture.CreateMinimalModule(_root);
        var backupDir = Path.Combine(_root, ".backups");

        var plan = MakeRenamePlan(moduleDir, "louis_roumain", "louis");
        plan.References.Add(new ResRefReference
        {
            FilePath = Path.Combine(moduleDir, "area01.git"),
            ResourceType = ResourceTypes.Git,
            Field = null,
            Location = "Creature List > Item 0 > TemplateResRef",
            OldValue = "louis_roumain",
            NewValue = "louis",
            ScopeTier = ResRefScopeTier.TypedGffField,
            IsSelected = false  // user unticked
        });

        var orchestrator = new ResRefRenameOrchestrator(new BackupService(backupDir));
        var result = await orchestrator.ExecuteAsync(new[] { plan }, "test");

        Assert.True(result.Success, $"Execute failed: {result.Error}");
        Assert.Equal(0, result.ReferencesUpdated);

        // GIT still has the OLD value (unticked reference was not applied)
        Assert.Equal("louis_roumain", GetGitCreatureTemplateResRef(Path.Combine(moduleDir, "area01.git")));
    }

    [Fact]
    public async Task ExecuteAsync_UntickedRenamePlan_FileNotRenamed()
    {
        var moduleDir = TestModuleFixture.CreateMinimalModule(_root);
        var backupDir = Path.Combine(_root, ".backups");

        var plan = MakeRenamePlan(moduleDir, "louis_roumain", "louis");
        plan.IsSelected = false;  // user unticked the entire rename

        var orchestrator = new ResRefRenameOrchestrator(new BackupService(backupDir));
        var result = await orchestrator.ExecuteAsync(new[] { plan }, "test");

        Assert.True(result.Success, $"Execute failed: {result.Error}");
        Assert.Equal(0, result.RenamedFiles);
        Assert.True(File.Exists(Path.Combine(moduleDir, "louis_roumain.utc")));
        Assert.False(File.Exists(Path.Combine(moduleDir, "louis.utc")));
    }

    [Fact]
    public async Task ExecuteAsync_FileModifiedBetweenPreviewAndExecute_AbortsWithoutChanges()
    {
        var moduleDir = TestModuleFixture.CreateMinimalModule(_root);
        var backupDir = Path.Combine(_root, ".backups");

        var plan = MakeRenamePlan(moduleDir, "louis_roumain", "louis");
        plan.References.Add(new ResRefReference
        {
            FilePath = Path.Combine(moduleDir, "area01.git"),
            ResourceType = ResourceTypes.Git,
            Field = null,
            Location = "Creature List > Item 0 > TemplateResRef",
            OldValue = "louis_roumain",
            NewValue = "louis",
            ScopeTier = ResRefScopeTier.TypedGffField,
            IsSelected = true
        });

        var snapshots = ResRefRenameOrchestrator.CaptureSnapshots(new[]
        {
            plan.SourceFilePath,
            Path.Combine(moduleDir, "area01.git")
        });

        // Simulate concurrent modification: bump area01.git's mtime forward
        File.SetLastWriteTimeUtc(Path.Combine(moduleDir, "area01.git"), DateTime.UtcNow.AddSeconds(5));

        var orchestrator = new ResRefRenameOrchestrator(new BackupService(backupDir));
        var result = await orchestrator.ExecuteAsync(new[] { plan }, "test", snapshots);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("modified between preview and execute", result.Error!);

        // No changes on disk — original file still exists, no rename happened
        Assert.True(File.Exists(plan.SourceFilePath));
        Assert.False(File.Exists(plan.TargetFilePath));

        // GIT reference also unchanged
        Assert.Equal("louis_roumain", GetGitCreatureTemplateResRef(Path.Combine(moduleDir, "area01.git")));
    }

    [Fact]
    public async Task ExecuteAsync_FailureDuringRename_RollsBackChanges()
    {
        var moduleDir = TestModuleFixture.CreateMinimalModule(_root);
        var backupDir = Path.Combine(_root, ".backups");

        // Pre-create the target file so File.Move throws (Windows: target must not exist).
        var plan = MakeRenamePlan(moduleDir, "louis_roumain", "louis");
        File.WriteAllText(plan.TargetFilePath, "preexisting");

        plan.References.Add(new ResRefReference
        {
            FilePath = Path.Combine(moduleDir, "area01.git"),
            ResourceType = ResourceTypes.Git,
            Field = null,
            Location = "Creature List > Item 0 > TemplateResRef",
            OldValue = "louis_roumain",
            NewValue = "louis",
            ScopeTier = ResRefScopeTier.TypedGffField,
            IsSelected = true
        });

        var orchestrator = new ResRefRenameOrchestrator(new BackupService(backupDir));
        var result = await orchestrator.ExecuteAsync(new[] { plan }, "test");

        Assert.False(result.Success);
        Assert.True(result.RollbackAttempted);
        Assert.True(result.RollbackSucceeded, "rollback should succeed cleanly");

        // Original file still exists at old path
        Assert.True(File.Exists(plan.SourceFilePath));

        // GIT reference reverted to old value (BackupService.RestoreAsync put it back)
        Assert.Equal("louis_roumain", GetGitCreatureTemplateResRef(Path.Combine(moduleDir, "area01.git")));
    }

    [Fact]
    public async Task ExecuteAsync_RestoreAfterRename_ProducesByteIdenticalModule()
    {
        var moduleDir = TestModuleFixture.CreateMinimalModule(_root);
        var backupDir = Path.Combine(_root, ".backups");

        // Snapshot the per-file bytes before the operation
        var beforeBytes = Directory.GetFiles(moduleDir)
            .ToDictionary(p => Path.GetFileName(p), p => File.ReadAllBytes(p));

        var plan = MakeRenamePlan(moduleDir, "louis_roumain", "louis");
        plan.References.Add(new ResRefReference
        {
            FilePath = Path.Combine(moduleDir, "area01.git"),
            ResourceType = ResourceTypes.Git,
            Field = null,
            Location = "Creature List > Item 0 > TemplateResRef",
            OldValue = "louis_roumain",
            NewValue = "louis",
            ScopeTier = ResRefScopeTier.TypedGffField,
            IsSelected = true
        });

        var backupService = new BackupService(backupDir);
        var orchestrator = new ResRefRenameOrchestrator(backupService);
        var result = await orchestrator.ExecuteAsync(new[] { plan }, "test");
        Assert.True(result.Success, $"Execute failed: {result.Error}");

        // Confirm the module changed
        var afterRenameGit = File.ReadAllBytes(Path.Combine(moduleDir, "area01.git"));
        Assert.NotEqual(beforeBytes["area01.git"], afterRenameGit);

        // Restore via BackupService — it restores file contents but NOT renames.
        // To round-trip we manually reverse the rename, then restore content.
        File.Move(plan.TargetFilePath, plan.SourceFilePath);

        var restoreOk = await backupService.RestoreAsync(result.BackupManifest!);
        Assert.True(restoreOk);

        // Now every file should be byte-identical to its pre-operation state.
        foreach (var kvp in beforeBytes)
        {
            var path = Path.Combine(moduleDir, kvp.Key);
            Assert.True(File.Exists(path), $"Expected restored file at {path}");
            Assert.Equal(kvp.Value, File.ReadAllBytes(path));
        }
    }
}
