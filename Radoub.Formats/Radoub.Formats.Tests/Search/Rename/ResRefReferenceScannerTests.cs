using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Search.Rename;
using Xunit;

namespace Radoub.Formats.Tests.Search.Rename;

public class ResRefReferenceScannerTests
{
    [Fact]
    public void Scan_UtcConversationField_FindsReference()
    {
        var gff = TestGffBuilder.MakeUtc(conversation: "louis_roumain");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utc, oldResRef: "louis_roumain", filePath: "/m/test.utc");

        Assert.Single(refs);
        Assert.Equal("Conversation", refs[0].Field?.Name);
        Assert.Equal("louis_roumain", refs[0].OldValue);
        Assert.Equal(ResRefScopeTier.TypedGffField, refs[0].ScopeTier);
    }

    [Theory]
    [InlineData("Louis_Roumain")]
    [InlineData("LOUIS_ROUMAIN")]
    [InlineData("louis_roumain")]
    public void Scan_FindsCaseInsensitiveMatches(string actualValueInGff)
    {
        var gff = TestGffBuilder.MakeUtc(conversation: actualValueInGff);
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utc, oldResRef: "louis_roumain", filePath: "/m/test.utc");

        Assert.Single(refs);
        Assert.Equal(actualValueInGff, refs[0].OldValue);  // preserves original case in OldValue
    }

    [Fact]
    public void Scan_TypedField_DoesNotMatchSubstrings()
    {
        var gff = TestGffBuilder.MakeUtc(conversation: "louis_roumain_extra");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utc, oldResRef: "louis_roumain", filePath: "/m/test.utc");

        Assert.Empty(refs);
    }

    [Fact]
    public void Scan_UtiOnAcquireScript_FindsReference()
    {
        var gff = TestGffBuilder.MakeUti(onAcquireScript: "open_door");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Uti, oldResRef: "open_door", filePath: "/m/key.uti");

        Assert.Single(refs);
        Assert.NotNull(refs[0].Field);
        Assert.Equal(SearchFieldType.Script, refs[0].Field!.FieldType);
        Assert.Equal(SearchFieldCategory.Script, refs[0].Field!.Category);
    }

    [Theory]
    [InlineData("OnEnter")]
    [InlineData("OnExit")]
    [InlineData("OnHeartbeat")]
    [InlineData("OnUserDefined")]
    public void Scan_AreScriptField_FindsReference(string fieldName)
    {
        var gff = TestGffBuilder.MakeAreWithScriptField(fieldName, "test_script");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Are, oldResRef: "test_script", filePath: "/m/area.are");

        Assert.Single(refs);
        Assert.Equal(fieldName, refs[0].Field?.Name);
    }

    [Fact]
    public void Scan_UtdConversation_FindsReference()
    {
        var gff = TestGffBuilder.MakeUtd(conversation: "door_dialog");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utd, oldResRef: "door_dialog", filePath: "/m/door.utd");

        Assert.Single(refs);
        Assert.Equal("Conversation", refs[0].Field?.Name);
    }

    [Theory]
    [InlineData("OnOpen")]
    [InlineData("OnLock")]
    [InlineData("OnUnlock")]
    public void Scan_UtdScriptField_FindsReference(string fieldName)
    {
        var gff = TestGffBuilder.MakeUtdWithScriptField(fieldName, "door_script");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utd, oldResRef: "door_script", filePath: "/m/door.utd");

        Assert.Single(refs);
        Assert.Equal(fieldName, refs[0].Field?.Name);
    }

    [Fact]
    public void Scan_GitCreatureListInstances_FindsAllReferences()
    {
        var gff = TestGffBuilder.MakeGitWithList("Creature List", "TemplateResRef",
            "louis", "alice", "louis", "bob");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Git, oldResRef: "louis", filePath: "/m/area.git");

        Assert.Equal(2, refs.Count);
        Assert.All(refs, r => Assert.Equal("louis", r.OldValue.ToLowerInvariant()));
        Assert.All(refs, r => Assert.Contains("Creature List", r.Location));
    }
}
