using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

/// <summary>
/// Tests for ItpSearchProvider.Replace (#2178 follow-up).
///
/// The standard (non-rename) Marlinspike replace path goes through
/// BatchReplaceService → provider.Replace. ItpSearchProvider.Replace
/// was previously stubbed out ("ITP palette replace not yet supported"),
/// causing ITP files to silently skip even when their search matches were
/// part of the preview.
/// </summary>
public class ItpSearchProviderReplaceTests
{
    private static readonly FieldDefinition ResRefField = new()
    {
        Name = "ResRef",
        GffPath = "RESREF",
        FieldType = SearchFieldType.ResRef,
        Category = SearchFieldCategory.Identity,
        Description = "Blueprint resource reference",
        IsReplaceable = false  // requires allowResRefReplace
    };

    private static readonly FieldDefinition BlueprintNameField = new()
    {
        Name = "Name",
        GffPath = "NAME",
        FieldType = SearchFieldType.Text,
        Category = SearchFieldCategory.Content,
        Description = "Blueprint name"
    };

    [Fact]
    public void Replace_FlatItpResRefMatch_UpdatesBlueprintResRef()
    {
        var gff = BuildFlatItp(("blackbear", "Black Bear"), ("wolf", "Wolf"));
        var op = new ReplaceOperation
        {
            Match = new SearchMatch
            {
                Field = ResRefField,
                MatchedText = "blackbear",
                FullFieldValue = "blackbear",
                MatchOffset = 0,
                MatchLength = "blackbear".Length,
                Location = new ItpMatchLocation
                {
                    NodeType = ItpNodeType.Blueprint,
                    DisplayPath = "Bears > Black Bear"
                }
            },
            ReplacementText = "newbear99",
            AllowResRefReplace = true
        };

        var provider = new ItpSearchProvider();
        var results = provider.Replace(gff, new[] { op });

        var result = Assert.Single(results);
        Assert.True(result.Success, $"Replace skipped: {result.SkipReason}");

        var main = gff.RootStruct.GetField("MAIN")!.Value as GffList;
        Assert.Equal("newbear99", main!.Elements[0].GetField("RESREF")?.Value);
        Assert.Equal("wolf", main.Elements[1].GetField("RESREF")?.Value);  // sibling untouched
    }

    [Fact]
    public void Replace_NestedItpResRefMatch_UpdatesBlueprintResRef()
    {
        // MAIN[0] = category with LIST[ blueprint("blackbear","Black Bear") ]
        var blueprint = MakeBlueprint("blackbear", "Black Bear");
        var category = new GffStruct { Type = 1 };
        GffFieldBuilder.AddByteField(category, "ID", 0);
        GffFieldBuilder.AddListField(category, "LIST", new[] { blueprint });
        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddListField(root, "MAIN", new[] { category });
        var gff = new GffFile { FileType = "ITP ", FileVersion = "V3.2", RootStruct = root };

        var op = new ReplaceOperation
        {
            Match = new SearchMatch
            {
                Field = ResRefField,
                MatchedText = "blackbear",
                FullFieldValue = "blackbear",
                MatchOffset = 0,
                MatchLength = "blackbear".Length,
                Location = new ItpMatchLocation
                {
                    NodeType = ItpNodeType.Blueprint,
                    DisplayPath = "Bears > Black Bear"
                }
            },
            ReplacementText = "newbear",
            AllowResRefReplace = true
        };

        var results = new ItpSearchProvider().Replace(gff, new[] { op });

        Assert.True(results[0].Success);
        var main = gff.RootStruct.GetField("MAIN")!.Value as GffList;
        var nestedList = main!.Elements[0].GetField("LIST")!.Value as GffList;
        Assert.Equal("newbear", nestedList!.Elements[0].GetField("RESREF")?.Value);
    }

    [Fact]
    public void Replace_NoMatchingBlueprint_ReturnsSkipped()
    {
        var gff = BuildFlatItp(("wolf", "Wolf"));
        var op = new ReplaceOperation
        {
            Match = new SearchMatch
            {
                Field = ResRefField,
                MatchedText = "blackbear",
                FullFieldValue = "blackbear",
                MatchOffset = 0,
                MatchLength = "blackbear".Length,
                Location = new ItpMatchLocation
                {
                    NodeType = ItpNodeType.Blueprint,
                    DisplayPath = "Bears > Black Bear"
                }
            },
            ReplacementText = "newbear99",
            AllowResRefReplace = true
        };

        var results = new ItpSearchProvider().Replace(gff, new[] { op });
        Assert.False(results[0].Success);
    }

    [Fact]
    public void Replace_BlueprintNameField_UpdatesName()
    {
        // Name field replace is allowed (no allowResRefReplace required).
        var gff = BuildFlatItp(("blackbear", "Black Bear"));
        var op = new ReplaceOperation
        {
            Match = new SearchMatch
            {
                Field = BlueprintNameField,
                MatchedText = "Black",
                FullFieldValue = "Black Bear",
                MatchOffset = 0,
                MatchLength = "Black".Length,
                Location = new ItpMatchLocation
                {
                    NodeType = ItpNodeType.Blueprint,
                    DisplayPath = "Bears > Black Bear"
                }
            },
            ReplacementText = "Grizzly",
            AllowResRefReplace = false
        };

        var results = new ItpSearchProvider().Replace(gff, new[] { op });

        Assert.True(results[0].Success);
        var main = gff.RootStruct.GetField("MAIN")!.Value as GffList;
        Assert.Equal("Grizzly Bear", main!.Elements[0].GetField("NAME")?.Value);
        Assert.Equal("blackbear", main.Elements[0].GetField("RESREF")?.Value);  // ResRef unchanged
    }

    [Fact]
    public void Replace_SubstringMatchInResRef_UpdatesWithOffsetReplacement()
    {
        // ResRef = "blackbear_old", search matches "old" at offset 10.
        var gff = BuildFlatItp(("blackbear_old", null));
        var op = new ReplaceOperation
        {
            Match = new SearchMatch
            {
                Field = ResRefField,
                MatchedText = "old",
                FullFieldValue = "blackbear_old",
                MatchOffset = 10,
                MatchLength = 3,
                Location = new ItpMatchLocation
                {
                    NodeType = ItpNodeType.Blueprint,
                    DisplayPath = "Bears > Black Bear Old"
                }
            },
            ReplacementText = "new",
            AllowResRefReplace = true
        };

        var results = new ItpSearchProvider().Replace(gff, new[] { op });

        Assert.True(results[0].Success);
        var main = gff.RootStruct.GetField("MAIN")!.Value as GffList;
        Assert.Equal("blackbear_new", main!.Elements[0].GetField("RESREF")?.Value);
    }

    // --- Test fixtures ---

    private static GffStruct MakeBlueprint(string resRef, string? name)
    {
        var s = new GffStruct { Type = 0 };
        GffFieldBuilder.AddCResRefField(s, "RESREF", resRef);
        if (name != null) GffFieldBuilder.AddCExoStringField(s, "NAME", name);
        return s;
    }

    private static GffFile BuildFlatItp(params (string resRef, string? name)[] blueprints)
    {
        var nodes = new List<GffStruct>();
        foreach (var (rr, nm) in blueprints)
            nodes.Add(MakeBlueprint(rr, nm));

        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddListField(root, "MAIN", nodes);
        return new GffFile { FileType = "ITP ", FileVersion = "V3.2", RootStruct = root };
    }
}
