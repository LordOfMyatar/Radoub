using System.Text;
using Radoub.Formats.TwoDA;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for TwoDAWriter 2DA persistence (issue #2271).
/// Formatting mirrors neverwinter.nim twoda.nim writeTwoDA: "2DA V2.0" header,
/// optional DEFAULT line, column-aligned rows, **** for null cells, CP-1252,
/// row indices in the leftmost unnamed column. CRLF line endings.
/// </summary>
public class TwoDAWriterTests
{
    static TwoDAWriterTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static TwoDAFile Build(params string?[][] rows)
    {
        var twoDA = new TwoDAFile { FileType = "2DA", FileVersion = "V2.0" };
        twoDA.Columns.AddRange(new[] { "Col1", "Col2" });
        for (int i = 0; i < rows.Length; i++)
        {
            var row = new TwoDARow { Label = i.ToString() };
            row.Values.AddRange(rows[i]);
            twoDA.Rows.Add(row);
        }
        return twoDA;
    }

    [Fact]
    public void Write_MatchesNeverwinterNimColumnAlignment()
    {
        var twoDA = Build(
            new[] { "A", "B" },
            new[] { "C", "DD" });

        var text = TwoDAWriter.WriteString(twoDA);

        // idWidth = max(3, len("2")) = 3; maxColWidth = [4, 4].
        var expected =
            "2DA V2.0\r\n" +
            "\r\n" +                         // empty DEFAULT line (no default present)
            "     Col1     Col2\r\n" +
            "0    A        B\r\n" +
            "1    C        DD\r\n";

        Assert.Equal(expected, text);
    }

    [Fact]
    public void Write_NullCell_EmittedAsStars()
    {
        var twoDA = Build(new string?[] { "A", null });

        var text = TwoDAWriter.WriteString(twoDA);

        Assert.Contains("****", text);
    }

    [Fact]
    public void Write_WithDefaultValue_EmitsDefaultLine()
    {
        var twoDA = Build(new[] { "A", "B" });
        twoDA.DefaultValue = "fallback";

        var text = TwoDAWriter.WriteString(twoDA);

        Assert.Contains("DEFAULT: fallback\r\n", text);
    }

    [Fact]
    public void Write_ValueWithWhitespace_IsQuoted()
    {
        var twoDA = Build(new[] { "hello world", "B" });

        var text = TwoDAWriter.WriteString(twoDA);

        Assert.Contains("\"hello world\"", text);
    }

    [Fact]
    public void Write_NoColumns_Throws()
    {
        var twoDA = new TwoDAFile();
        Assert.Throws<InvalidOperationException>(() => TwoDAWriter.WriteString(twoDA));
    }

    [Fact]
    public void Write_NullFile_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TwoDAWriter.Write(null!));
    }

    // --- Round-trip ---

    [Fact]
    public void RoundTrip_PreservesColumnsAndCells()
    {
        var twoDA = Build(
            new[] { "sword", "100" },
            new string?[] { "axe", null },
            new[] { "bow", "50" });

        var reparsed = TwoDAReader.Read(TwoDAWriter.Write(twoDA));

        Assert.Equal(2, reparsed.ColumnCount);
        Assert.Equal("Col1", reparsed.Columns[0]);
        Assert.Equal("Col2", reparsed.Columns[1]);
        Assert.Equal(3, reparsed.RowCount);
        Assert.Equal("sword", reparsed.GetValue(0, "Col1"));
        Assert.Equal("100", reparsed.GetValue(0, "Col2"));
        Assert.Null(reparsed.Rows[1].Values[1]); // **** cell
        Assert.Equal("bow", reparsed.GetValue(2, "Col1"));
    }

    [Fact]
    public void RoundTrip_PreservesDefaultValue()
    {
        var twoDA = Build(new[] { "A", "B" });
        twoDA.DefaultValue = "9";

        var reparsed = TwoDAReader.Read(TwoDAWriter.Write(twoDA));

        Assert.Equal("9", reparsed.DefaultValue);
    }

    [Fact]
    public void RoundTrip_QuotedWhitespaceValue_Preserved()
    {
        var twoDA = Build(new[] { "two words", "B" });

        var reparsed = TwoDAReader.Read(TwoDAWriter.Write(twoDA));

        Assert.Equal("two words", reparsed.GetValue(0, "Col1"));
    }

    [Fact]
    public void RoundTrip_StableAcrossTwoWrites()
    {
        var twoDA = Build(
            new[] { "alpha", "1" },
            new[] { "beta", "22" });

        var firstPass = TwoDAWriter.Write(twoDA);
        var secondPass = TwoDAWriter.Write(TwoDAReader.Read(firstPass));

        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void Write_Cp1252Encoding_HighByteRoundTrips()
    {
        var twoDA = Build(new[] { "café", "B" }); // é = 0xE9 in CP-1252

        var bytes = TwoDAWriter.Write(twoDA);
        var reparsed = TwoDAReader.Read(bytes);

        Assert.Equal("café", reparsed.GetValue(0, "Col1"));
    }

    [Fact]
    public void Write_DoubleQuoteInValue_Throws()
    {
        var twoDA = Build(new[] { "he said \"hi\"", "B" });

        Assert.Throws<InvalidOperationException>(() => TwoDAWriter.WriteString(twoDA));
    }

    // --- Stock BioWare 2DA round-trip (issue acceptance) ---

    /// <summary>
    /// Real BioWare feat.2da (49-column, ~1100-row table) sourced from the rollnw
    /// test corpus. Parsing then writing must preserve every cell, and a second
    /// write must be byte-stable (idempotent) once column widths are normalized.
    /// </summary>
    [Fact]
    public void RoundTrip_StockFeat2da_PreservesCellsAndIsStable()
    {
        const string fixturePath =
            @"d:\LOM\workspace\rollnw\tests\test_data\user\development\feat.2da";
        if (!File.Exists(fixturePath))
            return; // fixture unavailable in this checkout; covered by synthetic tests

        var original = TwoDAReader.Read(File.ReadAllBytes(fixturePath));

        var firstPass = TwoDAWriter.Write(original);
        var reparsed = TwoDAReader.Read(firstPass);

        // Cell-for-cell equality after a normalize→write→read cycle.
        Assert.Equal(original.ColumnCount, reparsed.ColumnCount);
        Assert.Equal(original.RowCount, reparsed.RowCount);
        for (int r = 0; r < original.RowCount; r++)
        {
            for (int c = 0; c < original.ColumnCount; c++)
                Assert.Equal(original.Rows[r].Values[c], reparsed.Rows[r].Values[c]);
        }

        // Idempotent: writing the reparsed table reproduces the same bytes.
        var secondPass = TwoDAWriter.Write(reparsed);
        Assert.Equal(firstPass, secondPass);
    }
}
