using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Search.Rename;
using Xunit;

namespace Radoub.Formats.Tests.Search.Rename;

/// <summary>
/// Tests for ITP (palette) reference scanning and application (#2178).
///
/// ITP files are GFF-based trees where leaf "blueprint" structs carry a
/// RESREF field pointing at a UTC/UTI/etc. file. Renaming a blueprint
/// must update every palette referencing it.
/// </summary>
public class ItpRenameTests
{
    // --- Scanner ---

    [Fact]
    public void Scan_FlatItpWithMatchingBlueprint_FindsReference()
    {
        var gff = BuildItp(
            Blueprint("louis_roumain"),
            Blueprint("alice_smith"));

        var refs = new ResRefReferenceScanner()
            .Scan(gff, ResourceTypes.Itp, oldResRef: "louis_roumain", filePath: "/m/itemref.itp");

        Assert.Single(refs);
        Assert.Equal("louis_roumain", refs[0].OldValue);
        Assert.Equal(ResourceTypes.Itp, refs[0].ResourceType);
        Assert.Equal(ResRefScopeTier.TypedGffField, refs[0].ScopeTier);
    }

    [Fact]
    public void Scan_NestedItpWithMatchingBlueprintInCategory_FindsReference()
    {
        // MAIN > Category(0) > LIST > [ Blueprint("louis_roumain") ]
        var category = new GffStruct { Type = 1 };
        GffFieldBuilder.AddByteField(category, "ID", 0);
        GffFieldBuilder.AddListField(category, "LIST", new[] { MakeBlueprint("louis_roumain") });

        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddListField(root, "MAIN", new[] { category });
        var gff = new GffFile { FileType = "ITP ", FileVersion = "V3.2", RootStruct = root };

        var refs = new ResRefReferenceScanner()
            .Scan(gff, ResourceTypes.Itp, oldResRef: "louis_roumain", filePath: "/m/test.itp");

        Assert.Single(refs);
        Assert.Equal("louis_roumain", refs[0].OldValue);
    }

    [Fact]
    public void Scan_NoMatchingBlueprint_ReturnsEmpty()
    {
        var gff = BuildItp(Blueprint("alice_smith"), Blueprint("bob_jones"));

        var refs = new ResRefReferenceScanner()
            .Scan(gff, ResourceTypes.Itp, oldResRef: "louis_roumain", filePath: "/m/test.itp");

        Assert.Empty(refs);
    }

    [Fact]
    public void Scan_MultipleMatches_ReturnsAll()
    {
        // Two blueprints with the same RESREF (palette duplicates are possible).
        var gff = BuildItp(
            Blueprint("louis_roumain"),
            Blueprint("louis_roumain"));

        var refs = new ResRefReferenceScanner()
            .Scan(gff, ResourceTypes.Itp, oldResRef: "louis_roumain", filePath: "/m/test.itp");

        Assert.Equal(2, refs.Count);
        Assert.All(refs, r => Assert.Equal("louis_roumain", r.OldValue));
    }

    [Fact]
    public void Scan_CaseInsensitiveMatch_PreservesOriginalCaseInOldValue()
    {
        var gff = BuildItp(Blueprint("Louis_Roumain"));

        var refs = new ResRefReferenceScanner()
            .Scan(gff, ResourceTypes.Itp, oldResRef: "louis_roumain", filePath: "/m/test.itp");

        Assert.Single(refs);
        Assert.Equal("Louis_Roumain", refs[0].OldValue);  // preserves original case
    }

    // --- Round-trip: scan + apply via Radoub.UI applier (tested in Radoub.UI.Tests) ---
    //
    // The applier itself lives in Radoub.UI (GffReferenceLocationApplier) so its
    // round-trip test lives there. Here we only verify the scanner emits a
    // Location string the applier can recognize.

    [Fact]
    public void Scan_Reference_HasItpLocationPrefix()
    {
        var gff = BuildItp(Blueprint("louis_roumain"));

        var refs = new ResRefReferenceScanner()
            .Scan(gff, ResourceTypes.Itp, oldResRef: "louis_roumain", filePath: "/m/test.itp");

        Assert.Single(refs);
        Assert.StartsWith("MAIN", refs[0].Location);
        Assert.EndsWith("RESREF", refs[0].Location);
    }

    // --- Test fixtures ---

    private static GffStruct MakeBlueprint(string resRef)
    {
        var s = new GffStruct { Type = 0 };
        GffFieldBuilder.AddCResRefField(s, "RESREF", resRef);
        return s;
    }

    private static GffStruct Blueprint(string resRef) => MakeBlueprint(resRef);

    /// <summary>Build a minimal flat ITP: MAIN list contains blueprint structs directly.</summary>
    private static GffFile BuildItp(params GffStruct[] blueprints)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddListField(root, "MAIN", blueprints);
        return new GffFile { FileType = "ITP ", FileVersion = "V3.2", RootStruct = root };
    }
}
