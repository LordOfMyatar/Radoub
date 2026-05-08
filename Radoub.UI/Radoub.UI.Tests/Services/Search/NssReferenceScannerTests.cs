using Radoub.Formats.Common;
using Radoub.Formats.Search.Rename;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class NssReferenceScannerTests : IDisposable
{
    private readonly string _tempDir;

    public NssReferenceScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nss-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteNss(string fileName, string source)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, source);
        return path;
    }

    [Fact]
    public void Scan_QuotedMatch_EmitsHighConfidenceRow()
    {
        var path = WriteNss("script.nss",
            "void main() {\n  CreateObject(OBJECT_TYPE_CREATURE, \"louis_roumain\", GetLocation(OBJECT_SELF));\n}\n");

        var scanner = new NssReferenceScanner();
        var refs = scanner.Scan(path, oldResRef: "louis_roumain");

        Assert.Single(refs);
        Assert.Equal(ResRefScopeTier.NssQuotedString, refs[0].ScopeTier);
        Assert.Equal("louis_roumain", refs[0].OldValue);
    }
}
