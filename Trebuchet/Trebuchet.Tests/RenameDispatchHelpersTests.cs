using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Search.Rename;
using Radoub.UI.Services.Search;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Unit tests for the rename dispatch helpers extracted from MarlinspikePanel.
/// These cover the branching logic that converts a BatchReplacePreview with
/// filename matches into ResRefRenamePlans for the orchestrator.
/// </summary>
public class RenameDispatchHelpersTests : IDisposable
{
    private readonly string _tempDir;

    public RenameDispatchHelpersTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"rdh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // --- HasFilenameMatches ---

    [Fact]
    public void HasFilenameMatches_TrueWhenPreviewContainsFilenameField()
    {
        var preview = new BatchReplacePreview();
        preview.Changes.Add(new PendingChange
        {
            Match = MakeMatchOn(FilenameSearchProvider.FilenameField),
            ReplacementText = "x",
            FilePath = "/m/test.dlg"
        });

        Assert.True(RenameDispatchHelpers.HasFilenameMatches(preview));
    }

    [Fact]
    public void HasFilenameMatches_FalseWhenPreviewHasOnlyContentFields()
    {
        var contentField = new FieldDefinition
        {
            Name = "Tag",
            GffPath = "Tag",
            FieldType = SearchFieldType.Tag,
            Category = SearchFieldCategory.Identity
        };
        var preview = new BatchReplacePreview();
        preview.Changes.Add(new PendingChange
        {
            Match = MakeMatchOn(contentField),
            ReplacementText = "x",
            FilePath = "/m/test.utc"
        });

        Assert.False(RenameDispatchHelpers.HasFilenameMatches(preview));
    }

    // --- BuildExistingResRefIndex ---

    [Fact]
    public void BuildExistingResRefIndex_GroupsByExtension()
    {
        File.WriteAllText(Path.Combine(_tempDir, "louis.dlg"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "louis.utc"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "alice.utc"), "x");

        var index = RenameDispatchHelpers.BuildExistingResRefIndex(_tempDir);

        Assert.Contains("louis", index[".dlg"]);
        Assert.Contains("louis", index[".utc"]);
        Assert.Contains("alice", index[".utc"]);
        Assert.Single(index[".dlg"]);
        Assert.Equal(2, index[".utc"].Count);
    }

    // --- ApplyReplacement ---

    [Fact]
    public void ApplyReplacement_ReplacesAtMatchOffset()
    {
        var match = new SearchMatch
        {
            Field = FilenameSearchProvider.FilenameField,
            MatchedText = "louis",
            FullFieldValue = "louis_roumain",
            MatchOffset = 0,
            MatchLength = "louis".Length,
            Location = "test"
        };

        var result = RenameDispatchHelpers.ApplyReplacement("louis_roumain", match, "alice");

        Assert.Equal("alice_roumain", result);
    }

    // --- BuildRenamePlansFromPreview ---

    [Fact]
    public void BuildRenamePlansFromPreview_SkipsInvalidValidations()
    {
        File.WriteAllText(Path.Combine(_tempDir, "valid_name.dlg"), "x");

        var preview = new BatchReplacePreview();
        preview.Changes.Add(new PendingChange
        {
            Match = new SearchMatch
            {
                Field = FilenameSearchProvider.FilenameField,
                MatchedText = "valid_name",
                FullFieldValue = "valid_name",
                MatchOffset = 0,
                MatchLength = "valid_name".Length,
                Location = "test"
            },
            ReplacementText = "way-too-long-for-aurora-which-fails-16-char-limit",
            FilePath = Path.Combine(_tempDir, "valid_name.dlg")
        });

        var plans = RenameDispatchHelpers.BuildRenamePlansFromPreview(
            preview, _tempDir, new ResRefValidator());

        Assert.Empty(plans);  // validator rejects > 16 chars; plan skipped
    }

    [Fact]
    public void BuildRenamePlansFromPreview_BuildsValidPlanForSimpleRename()
    {
        // Create the file being renamed and one unrelated file in the directory.
        File.WriteAllText(Path.Combine(_tempDir, "louis.dlg"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "alice.utc"), "x");

        var preview = new BatchReplacePreview();
        preview.Changes.Add(new PendingChange
        {
            Match = new SearchMatch
            {
                Field = FilenameSearchProvider.FilenameField,
                MatchedText = "louis",
                FullFieldValue = "louis",
                MatchOffset = 0,
                MatchLength = "louis".Length,
                Location = "test"
            },
            ReplacementText = "bob",
            FilePath = Path.Combine(_tempDir, "louis.dlg")
        });

        var plans = RenameDispatchHelpers.BuildRenamePlansFromPreview(
            preview, _tempDir, new ResRefValidator());

        var plan = Assert.Single(plans);
        Assert.Equal("louis", plan.OldName);
        Assert.Equal("bob", plan.NewName);
        Assert.Equal(ResourceTypes.Dlg, plan.ResourceType);
        Assert.Equal(Path.Combine(_tempDir, "louis.dlg"), plan.SourceFilePath);
        Assert.Equal(Path.Combine(_tempDir, "bob.dlg"), plan.TargetFilePath);
    }

    // --- PopulateReferencesAsync ---

    [Fact]
    public async Task PopulateReferencesAsync_HonorsFileTypeFilter()
    {
        // UTC referencing "louis" via Conversation field (top-level scalar)
        var utc = MakeUtcWithConversation("louis");
        File.WriteAllBytes(Path.Combine(_tempDir, "test.utc"), GffWriter.Write(utc));

        // GIT with a Creature List entry referencing "louis"
        var git = MakeGitWithCreature("louis");
        File.WriteAllBytes(Path.Combine(_tempDir, "area.git"), GffWriter.Write(git));

        var plan = new ResRefRenamePlan
        {
            OldName = "louis",
            NewName = "alice",
            ResourceType = ResourceTypes.Dlg,
            Validation = ResRefValidationResult.Ok("alice"),
            SourceFilePath = "/dummy",
            TargetFilePath = "/dummy"
        };

        // Filter excludes GIT → only UTC references should be picked up
        var criteria = new SearchCriteria
        {
            Pattern = "louis",
            FileTypeFilter = new[] { ResourceTypes.Utc }
        };

        await RenameDispatchHelpers.PopulateReferencesAsync(
            new[] { plan }, _tempDir, includeNss: false, criteria);

        Assert.All(plan.References, r => Assert.NotEqual(ResourceTypes.Git, r.ResourceType));
        Assert.Contains(plan.References, r => r.ResourceType == ResourceTypes.Utc);
    }

    // --- Test fixtures ---

    private static SearchMatch MakeMatchOn(FieldDefinition field) => new()
    {
        Field = field,
        MatchedText = "x",
        FullFieldValue = "x",
        MatchOffset = 0,
        MatchLength = 1,
        Location = "test"
    };

    /// <summary>Build a minimal UTC GFF with a Conversation ResRef field.</summary>
    private static GffFile MakeUtcWithConversation(string conversation)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddCResRefField(root, "Conversation", conversation);
        return new GffFile { FileType = "UTC ", FileVersion = "V3.2", RootStruct = root };
    }

    /// <summary>Build a minimal GIT GFF with one Creature List entry referencing the given ResRef.</summary>
    private static GffFile MakeGitWithCreature(string templateResRef)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var creature = new GffStruct { Type = 0 };
        GffFieldBuilder.AddCResRefField(creature, "TemplateResRef", templateResRef);
        GffFieldBuilder.AddListField(root, "Creature List", new[] { creature });
        return new GffFile { FileType = "GIT ", FileVersion = "V3.2", RootStruct = root };
    }
}
