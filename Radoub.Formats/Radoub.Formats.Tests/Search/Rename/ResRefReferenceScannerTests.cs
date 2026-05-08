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
}
