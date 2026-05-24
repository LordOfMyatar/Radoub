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

    [Fact]
    public void Scan_BareSubstringMatch_EmitsLowConfidenceRow()
    {
        var path = WriteNss("comment.nss",
            "// Reminder: louis_roumain is the merchant in town\nvoid main() {}\n");

        var scanner = new NssReferenceScanner();
        var refs = scanner.Scan(path, oldResRef: "louis_roumain");

        Assert.Single(refs);
        Assert.Equal(ResRefScopeTier.NssBareSubstring, refs[0].ScopeTier);
    }

    [Fact]
    public void Scan_BothQuotedAndBare_EmitsBothWithCorrectTiers()
    {
        var path = WriteNss("mix.nss",
            "// louis_roumain comment\nCreateObject(0, \"louis_roumain\", l);\n");

        var scanner = new NssReferenceScanner();
        var refs = scanner.Scan(path, oldResRef: "louis_roumain");

        Assert.Equal(2, refs.Count);
        Assert.Contains(refs, r => r.ScopeTier == ResRefScopeTier.NssQuotedString);
        Assert.Contains(refs, r => r.ScopeTier == ResRefScopeTier.NssBareSubstring);
    }

    [Fact]
    public void Scan_BareSubstring_DoesNotDoubleCountQuoted()
    {
        // The substring `louis` appears inside `"louis"`. The bare-substring pass
        // must not report this as a separate low-confidence match — the quoted
        // pass already covers it.
        var path = WriteNss("only_quoted.nss",
            "Object o = GetObjectByTag(\"louis\");\n");

        var scanner = new NssReferenceScanner();
        var refs = scanner.Scan(path, oldResRef: "louis");

        Assert.Single(refs);
        Assert.Equal(ResRefScopeTier.NssQuotedString, refs[0].ScopeTier);
    }

    [Theory]
    [InlineData("\"Louis_Roumain\"")]
    [InlineData("\"LOUIS_ROUMAIN\"")]
    [InlineData("\"louis_roumain\"")]
    public void Scan_QuotedMatch_IsCaseInsensitive(string scriptSnippet)
    {
        var path = WriteNss("mixed.nss", $"void main() {{ string s = {scriptSnippet}; }}\n");

        var scanner = new NssReferenceScanner();
        var refs = scanner.Scan(path, oldResRef: "louis_roumain");

        Assert.Single(refs);
        Assert.Equal(ResRefScopeTier.NssQuotedString, refs[0].ScopeTier);
    }

    [Fact]
    public void Scan_EmptyFile_ReturnsNoResults()
    {
        var path = WriteNss("empty.nss", string.Empty);

        var scanner = new NssReferenceScanner();
        var refs = scanner.Scan(path, oldResRef: "louis");

        Assert.Empty(refs);
    }

    [Fact]
    public void Scan_EmptyPattern_ReturnsNoResults()
    {
        var path = WriteNss("script.nss", "void main() { string s = \"louis\"; }\n");

        var scanner = new NssReferenceScanner();
        var refs = scanner.Scan(path, oldResRef: "");

        Assert.Empty(refs);
    }

    [Fact]
    public void Scan_NonexistentFile_ReturnsNoResults()
    {
        var scanner = new NssReferenceScanner();
        var refs = scanner.Scan(Path.Combine(_tempDir, "missing.nss"), oldResRef: "louis");

        Assert.Empty(refs);
    }
}
