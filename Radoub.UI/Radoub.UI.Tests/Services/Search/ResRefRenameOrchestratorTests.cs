using Radoub.Formats.Common;
using Radoub.Formats.Gff;
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
}
