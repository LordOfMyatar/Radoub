using System.Text;
using Radoub.Formats.Erf;
using Radoub.Formats.Gff;
using Radoub.Formats.TwoDA;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Round-trip tests for NWN1 native Windows-1252 encoding across GFF, ERF, and 2DA.
/// Issue #2242 — writers used UTF-8 instead of CP-1252, corrupting accented characters
/// (é, ü, ß) and breaking in-game string lookup for vanilla NWN files.
///
/// Reference: neverwinter.nim util.nim:49 — getNwnEncoding default = "windows-1252".
/// </summary>
public class EncodingRoundTripTests
{
    static EncodingRoundTripTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private const string AccentedSample = "café—Mädchen—straße";

    // Pre-computed CP-1252 byte sequence for AccentedSample so the assertion is
    // independent of the SUT's own encoding choice.
    //   'café'    = 63 61 66 E9
    //   '—'       = 97 (CP-1252 em dash)
    //   'Mädchen' = 4D E4 64 63 68 65 6E
    //   '—'       = 97
    //   'straße'  = 73 74 72 61 DF 65
    private static readonly byte[] AccentedCp1252 = new byte[]
    {
        0x63, 0x61, 0x66, 0xE9,
        0x97,
        0x4D, 0xE4, 0x64, 0x63, 0x68, 0x65, 0x6E,
        0x97,
        0x73, 0x74, 0x72, 0x61, 0xDF, 0x65
    };

    // -----------------------------------------------------------------
    // GFF — CExoString
    // -----------------------------------------------------------------

    [Fact]
    public void GffWriter_CExoString_WritesCp1252Bytes()
    {
        var gff = BuildGffWithCExoString("Description", AccentedSample);

        var bytes = GffWriter.Write(gff);

        Assert.Contains(SearchableByteWindow(bytes), w => SequenceEqual(w, AccentedCp1252));
    }

    [Fact]
    public void GffRoundTrip_CExoString_PreservesAccentedChars()
    {
        var gff = BuildGffWithCExoString("Description", AccentedSample);

        var bytes = GffWriter.Write(gff);
        var parsed = GffReader.Read(bytes);

        var field = parsed.RootStruct.GetField("Description");
        Assert.NotNull(field);
        Assert.Equal(AccentedSample, field.Value);
    }

    [Fact]
    public void GffReader_CExoString_ReadsCp1252EncodedBytes()
    {
        var gff = BuildGffWithCExoString("Description", AccentedSample);
        var bytes = GffWriter.Write(gff);

        var parsed = GffReader.Read(bytes);

        Assert.Equal(AccentedSample, parsed.RootStruct.GetField("Description")!.Value);
    }

    // -----------------------------------------------------------------
    // GFF — CExoLocString
    // -----------------------------------------------------------------

    [Fact]
    public void GffWriter_CExoLocString_WritesCp1252Bytes()
    {
        var loc = new CExoLocString { StrRef = 0xFFFFFFFFu };
        loc.LocalizedStrings[0] = AccentedSample;
        var gff = BuildGffWithCExoLocString("LocName", loc);

        var bytes = GffWriter.Write(gff);

        Assert.Contains(SearchableByteWindow(bytes), w => SequenceEqual(w, AccentedCp1252));
    }

    [Fact]
    public void GffRoundTrip_CExoLocString_PreservesAccentedChars()
    {
        var loc = new CExoLocString { StrRef = 0xFFFFFFFFu };
        loc.LocalizedStrings[0] = AccentedSample;
        var gff = BuildGffWithCExoLocString("LocName", loc);

        var bytes = GffWriter.Write(gff);
        var parsed = GffReader.Read(bytes);

        var field = parsed.RootStruct.GetField("LocName");
        Assert.NotNull(field);
        var parsedLoc = Assert.IsType<CExoLocString>(field.Value);
        Assert.Equal(AccentedSample, parsedLoc.LocalizedStrings[0]);
    }

    // -----------------------------------------------------------------
    // ERF localized strings
    // -----------------------------------------------------------------

    [Fact]
    public void ErfWriter_LocalizedString_WritesCp1252Bytes()
    {
        var erf = new ErfFile { FileType = "MOD ", FileVersion = "V1.0" };
        erf.LocalizedStrings.Add(new ErfLocalizedString { LanguageId = 0, Text = AccentedSample });

        using var ms = new MemoryStream();
        ErfWriter.Write(erf, ms, new Dictionary<(string, ushort), byte[]>());
        var bytes = ms.ToArray();

        Assert.Contains(SearchableByteWindow(bytes), w => SequenceEqual(w, AccentedCp1252));
    }

    [Fact]
    public void ErfRoundTrip_LocalizedString_PreservesAccentedChars()
    {
        var erf = new ErfFile { FileType = "MOD ", FileVersion = "V1.0" };
        erf.LocalizedStrings.Add(new ErfLocalizedString { LanguageId = 0, Text = AccentedSample });

        using var ms = new MemoryStream();
        ErfWriter.Write(erf, ms, new Dictionary<(string, ushort), byte[]>());
        var parsed = ErfReader.Read(ms.ToArray());

        Assert.Single(parsed.LocalizedStrings);
        Assert.Equal(AccentedSample, parsed.LocalizedStrings[0].Text);
    }

    // -----------------------------------------------------------------
    // 2DA
    // -----------------------------------------------------------------

    [Fact]
    public void TwoDAReader_ReadsCp1252EncodedFile()
    {
        // Hand-build a 2DA byte stream in CP-1252.
        var cp1252 = Encoding.GetEncoding(1252);
        var content = "2DA V2.0\nLabel Name\n0 Mädchen\n1 straße";
        var bytes = cp1252.GetBytes(content);

        var twoDA = TwoDAReader.Read(bytes);

        Assert.Equal(2, twoDA.Rows.Count);
        Assert.Equal("Mädchen", twoDA.Rows[0].Values[0]);
        Assert.Equal("straße", twoDA.Rows[1].Values[0]);
    }

    [Fact]
    public void TwoDAReader_PreservesUtf8WhenBomPresent()
    {
        // UTF-8 BOM = EF BB BF. If present, reader treats input as UTF-8 so
        // hand-edited UTF-8 2DAs (which some community tools produce) still parse.
        var content = "2DA V2.0\nLabel Name\n0 Mädchen";
        var utf8WithBom = new List<byte> { 0xEF, 0xBB, 0xBF };
        utf8WithBom.AddRange(Encoding.UTF8.GetBytes(content));

        var twoDA = TwoDAReader.Read(utf8WithBom.ToArray());

        Assert.Single(twoDA.Rows);
        Assert.Equal("Mädchen", twoDA.Rows[0].Values[0]);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static GffFile BuildGffWithCExoString(string label, string value)
    {
        var gff = new GffFile { FileType = "TST ", FileVersion = "V3.2" };
        gff.RootStruct.Type = 0xFFFFFFFF;
        gff.RootStruct.Fields.Add(new GffField
        {
            Label = label,
            Type = GffField.CExoString,
            Value = value
        });
        return gff;
    }

    private static GffFile BuildGffWithCExoLocString(string label, CExoLocString value)
    {
        var gff = new GffFile { FileType = "TST ", FileVersion = "V3.2" };
        gff.RootStruct.Type = 0xFFFFFFFF;
        gff.RootStruct.Fields.Add(new GffField
        {
            Label = label,
            Type = GffField.CExoLocString,
            Value = value
        });
        return gff;
    }

    /// <summary>
    /// Enumerate sliding byte windows of the target length so we can search the
    /// full file for the expected CP-1252 sequence regardless of where the
    /// string field lands inside the FieldData block.
    /// </summary>
    private static IEnumerable<ArraySegment<byte>> SearchableByteWindow(byte[] buffer)
    {
        int windowLen = AccentedCp1252.Length;
        for (int i = 0; i + windowLen <= buffer.Length; i++)
            yield return new ArraySegment<byte>(buffer, i, windowLen);
    }

    private static bool SequenceEqual(ArraySegment<byte> window, byte[] expected)
    {
        if (window.Count != expected.Length) return false;
        for (int i = 0; i < expected.Length; i++)
            if (window.Array![window.Offset + i] != expected[i]) return false;
        return true;
    }
}
