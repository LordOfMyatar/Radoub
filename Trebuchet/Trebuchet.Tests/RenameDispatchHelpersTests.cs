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

    // #2182 — rejected entries report the specific validator reason so the UI can
    // show actionable text instead of "validator rejected all proposed names".
    [Fact]
    public void BuildRenamePlansFromPreview_CapturesRejectionReasons()
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
            ReplacementText = "bad-name",  // hyphen → invalid chars
            FilePath = Path.Combine(_tempDir, "valid_name.dlg")
        });

        var reasons = new List<string>();
        var plans = RenameDispatchHelpers.BuildRenamePlansFromPreview(
            preview, _tempDir, new ResRefValidator(), reasons);

        Assert.Empty(plans);
        Assert.Single(reasons);
        Assert.Contains("'-'", reasons[0]);            // names the bad char (#2182)
        Assert.Contains("bad-name", reasons[0]);       // names the offending file/name
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

    [Fact]
    public async Task PopulateReferencesAsync_AllowedFilePathsRestrictsToSelection()
    {
        // Two files in the module, both referencing "louis"
        var utcPath = Path.Combine(_tempDir, "test.utc");
        File.WriteAllBytes(utcPath, GffWriter.Write(MakeUtcWithConversation("louis")));

        var gitPath = Path.Combine(_tempDir, "area.git");
        File.WriteAllBytes(gitPath, GffWriter.Write(MakeGitWithCreature("louis")));

        var plan = new ResRefRenamePlan
        {
            OldName = "louis",
            NewName = "alice",
            ResourceType = ResourceTypes.Dlg,
            Validation = ResRefValidationResult.Ok("alice"),
            SourceFilePath = "/dummy",
            TargetFilePath = "/dummy"
        };
        var criteria = new SearchCriteria { Pattern = "louis" };

        // Surgical mode: user selected only the UTC; the GIT must be ignored entirely.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { utcPath };

        await RenameDispatchHelpers.PopulateReferencesAsync(
            new[] { plan }, _tempDir, includeNss: false, criteria, allowed);

        Assert.All(plan.References, r => Assert.NotEqual(ResourceTypes.Git, r.ResourceType));
        Assert.Contains(plan.References, r => r.ResourceType == ResourceTypes.Utc);
    }

    [Fact]
    public async Task PopulateReferencesAsync_NullAllowedFilePathsScansEverything()
    {
        // Regression: passing null for allowedFilePaths must preserve module-wide behavior.
        File.WriteAllBytes(Path.Combine(_tempDir, "test.utc"),
            GffWriter.Write(MakeUtcWithConversation("louis")));
        File.WriteAllBytes(Path.Combine(_tempDir, "area.git"),
            GffWriter.Write(MakeGitWithCreature("louis")));

        var plan = new ResRefRenamePlan
        {
            OldName = "louis",
            NewName = "alice",
            ResourceType = ResourceTypes.Dlg,
            Validation = ResRefValidationResult.Ok("alice"),
            SourceFilePath = "/dummy",
            TargetFilePath = "/dummy"
        };

        await RenameDispatchHelpers.PopulateReferencesAsync(
            new[] { plan }, _tempDir, includeNss: false, new SearchCriteria { Pattern = "louis" },
            allowedFilePaths: null);

        Assert.Contains(plan.References, r => r.ResourceType == ResourceTypes.Utc);
        Assert.Contains(plan.References, r => r.ResourceType == ResourceTypes.Git);
    }

    // --- BuildResidualPreview (#2178 follow-up) ---
    //
    // When the dispatch path runs rename for filename rows, non-filename content
    // rows in the same preview (e.g. ITP Name field matches) must still be
    // processed by the standard replace path. BuildResidualPreview produces a
    // preview containing only those non-filename rows, with file paths remapped
    // to post-rename targets when the source was renamed.

    [Fact]
    public void BuildResidualPreview_StripsFilenameRows()
    {
        var filenameChange = new PendingChange
        {
            Match = MakeMatchOn(FilenameSearchProvider.FilenameField),
            ReplacementText = "lewie",
            FilePath = "/m/louis.utc"
        };
        var nameField = new FieldDefinition
        {
            Name = "Name", GffPath = "NAME",
            FieldType = SearchFieldType.Text,
            Category = SearchFieldCategory.Content
        };
        var contentChange = new PendingChange
        {
            Match = MakeMatchOn(nameField),
            ReplacementText = "Lewie",
            FilePath = "/m/palette.itp"
        };

        var preview = new BatchReplacePreview { AllowResRefReplace = true };
        preview.Changes.Add(filenameChange);
        preview.Changes.Add(contentChange);

        var residual = RenameDispatchHelpers.BuildResidualPreview(
            preview, renameMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Single(residual.Changes);
        Assert.Same(contentChange.Match.Field, residual.Changes[0].Match.Field);
        Assert.Equal("/m/palette.itp", residual.Changes[0].FilePath);
        Assert.True(residual.AllowResRefReplace);
    }

    [Fact]
    public void BuildResidualPreview_RemapsFilePathsForRenamedFiles()
    {
        // A non-filename change on a file that's about to be renamed must
        // refer to the post-rename target path.
        var resRefField = new FieldDefinition
        {
            Name = "ResRef", GffPath = "RESREF",
            FieldType = SearchFieldType.ResRef,
            Category = SearchFieldCategory.Identity,
            IsReplaceable = false
        };
        var change = new PendingChange
        {
            Match = MakeMatchOn(resRefField),
            ReplacementText = "lewie",
            FilePath = "/m/louis.utc"
        };
        var preview = new BatchReplacePreview { AllowResRefReplace = true };
        preview.Changes.Add(change);

        var renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/m/louis.utc"] = "/m/lewie.utc"
        };

        var residual = RenameDispatchHelpers.BuildResidualPreview(preview, renameMap);

        Assert.Single(residual.Changes);
        Assert.Equal("/m/lewie.utc", residual.Changes[0].FilePath);
    }

    [Fact]
    public void BuildResidualPreview_PreservesAllowResRefReplaceFlag()
    {
        var preview = new BatchReplacePreview { AllowResRefReplace = true };
        var residual = RenameDispatchHelpers.BuildResidualPreview(
            preview, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        Assert.True(residual.AllowResRefReplace);

        var preview2 = new BatchReplacePreview { AllowResRefReplace = false };
        var residual2 = RenameDispatchHelpers.BuildResidualPreview(
            preview2, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        Assert.False(residual2.AllowResRefReplace);
    }

    [Fact]
    public void BuildResidualPreview_EmptyWhenAllRowsAreFilenameMatches()
    {
        var preview = new BatchReplacePreview();
        preview.Changes.Add(new PendingChange
        {
            Match = MakeMatchOn(FilenameSearchProvider.FilenameField),
            ReplacementText = "lewie",
            FilePath = "/m/louis.utc"
        });

        var residual = RenameDispatchHelpers.BuildResidualPreview(
            preview, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Empty(residual.Changes);
    }

    [Fact]
    public void BuildResidualPreview_PreservesIsSelectedState()
    {
        var nameField = new FieldDefinition
        {
            Name = "Name", GffPath = "NAME",
            FieldType = SearchFieldType.Text,
            Category = SearchFieldCategory.Content
        };
        var selectedChange = new PendingChange
        {
            Match = MakeMatchOn(nameField),
            ReplacementText = "x",
            FilePath = "/m/a.itp",
            IsSelected = true
        };
        var unselectedChange = new PendingChange
        {
            Match = MakeMatchOn(nameField),
            ReplacementText = "x",
            FilePath = "/m/b.itp",
            IsSelected = false
        };
        var preview = new BatchReplacePreview();
        preview.Changes.Add(selectedChange);
        preview.Changes.Add(unselectedChange);

        var residual = RenameDispatchHelpers.BuildResidualPreview(
            preview, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(2, residual.Changes.Count);
        Assert.Contains(residual.Changes, c => c.FilePath == "/m/a.itp" && c.IsSelected);
        Assert.Contains(residual.Changes, c => c.FilePath == "/m/b.itp" && !c.IsSelected);
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
