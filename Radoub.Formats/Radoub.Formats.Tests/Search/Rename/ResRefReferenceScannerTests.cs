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
}
