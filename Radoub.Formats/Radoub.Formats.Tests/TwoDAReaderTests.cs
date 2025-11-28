using Radoub.Formats.TwoDA;
using Xunit;

namespace Radoub.Formats.Tests;

public class TwoDAReaderTests
{
    [Fact]
    public void Parse_MinimalFile_ParsesCorrectly()
    {
        var content = "2DA V2.0\nCol1 Col2\n0 A B";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("2DA", result.FileType);
        Assert.Equal("V2.0", result.FileVersion);
        Assert.Equal(2, result.ColumnCount);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void Parse_WithDefaultValue_ParsesDefault()
    {
        var content = "2DA V2.0\nDEFAULT: fallback\nCol1\n0 A";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("fallback", result.DefaultValue);
    }

    [Fact]
    public void Parse_QuotedDefaultValue_ParsesWithoutQuotes()
    {
        var content = "2DA V2.0\nDEFAULT: \"default value\"\nCol1\n0 A";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("default value", result.DefaultValue);
    }

    [Fact]
    public void Parse_ColumnHeaders_ParsedCorrectly()
    {
        var content = "2DA V2.0\nLabel Name Type Value\n0 item sword 100";

        var result = TwoDAReader.Parse(content);

        Assert.Equal(4, result.ColumnCount);
        Assert.Equal("Label", result.Columns[0]);
        Assert.Equal("Name", result.Columns[1]);
        Assert.Equal("Type", result.Columns[2]);
        Assert.Equal("Value", result.Columns[3]);
    }

    [Fact]
    public void Parse_MultipleRows_AllParsed()
    {
        var content = "2DA V2.0\nCol1 Col2\n0 A B\n1 C D\n2 E F";

        var result = TwoDAReader.Parse(content);

        Assert.Equal(3, result.RowCount);
        Assert.Equal("0", result.Rows[0].Label);
        Assert.Equal("1", result.Rows[1].Label);
        Assert.Equal("2", result.Rows[2].Label);
    }

    [Fact]
    public void Parse_EmptyCell_ReturnsNull()
    {
        var content = "2DA V2.0\nCol1 Col2\n0 **** Value";

        var result = TwoDAReader.Parse(content);

        Assert.Null(result.Rows[0].Values[0]);
        Assert.Equal("Value", result.Rows[0].Values[1]);
    }

    [Fact]
    public void Parse_QuotedValue_PreservesSpaces()
    {
        var content = "2DA V2.0\nCol1 Col2\n0 \"Hello World\" Normal";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("Hello World", result.Rows[0].Values[0]);
        Assert.Equal("Normal", result.Rows[0].Values[1]);
    }

    [Fact]
    public void Parse_ShortRow_PaddedWithNulls()
    {
        var content = "2DA V2.0\nCol1 Col2 Col3\n0 A";

        var result = TwoDAReader.Parse(content);

        Assert.Equal(3, result.Rows[0].Values.Count);
        Assert.Equal("A", result.Rows[0].Values[0]);
        Assert.Null(result.Rows[0].Values[1]);
        Assert.Null(result.Rows[0].Values[2]);
    }

    [Fact]
    public void Parse_TabSeparated_ParsesCorrectly()
    {
        var content = "2DA V2.0\nCol1\tCol2\n0\tA\tB";

        var result = TwoDAReader.Parse(content);

        Assert.Equal(2, result.ColumnCount);
        Assert.Equal("A", result.Rows[0].Values[0]);
        Assert.Equal("B", result.Rows[0].Values[1]);
    }

    [Fact]
    public void Parse_EmptyLines_Skipped()
    {
        var content = "2DA V2.0\n\nCol1 Col2\n\n0 A B\n\n1 C D\n";

        var result = TwoDAReader.Parse(content);

        Assert.Equal(2, result.RowCount);
    }

    [Fact]
    public void Parse_WindowsLineEndings_ParsesCorrectly()
    {
        var content = "2DA V2.0\r\nCol1 Col2\r\n0 A B";

        var result = TwoDAReader.Parse(content);

        Assert.Equal(2, result.ColumnCount);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void GetValue_ByColumnName_ReturnsValue()
    {
        var content = "2DA V2.0\nName Type\n0 Sword Weapon";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("Sword", result.GetValue(0, "Name"));
        Assert.Equal("Weapon", result.GetValue(0, "Type"));
    }

    [Fact]
    public void GetValue_ByColumnIndex_ReturnsValue()
    {
        var content = "2DA V2.0\nName Type\n0 Sword Weapon";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("Sword", result.GetValue(0, 0));
        Assert.Equal("Weapon", result.GetValue(0, 1));
    }

    [Fact]
    public void GetValue_CaseInsensitiveColumn_ReturnsValue()
    {
        var content = "2DA V2.0\nNAME Type\n0 Sword Weapon";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("Sword", result.GetValue(0, "name"));
        Assert.Equal("Sword", result.GetValue(0, "NAME"));
        Assert.Equal("Sword", result.GetValue(0, "Name"));
    }

    [Fact]
    public void GetValue_InvalidRow_ReturnsDefault()
    {
        var content = "2DA V2.0\nDEFAULT: N/A\nCol1\n0 A";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("N/A", result.GetValue(999, "Col1"));
    }

    [Fact]
    public void GetValue_InvalidColumn_ReturnsDefault()
    {
        var content = "2DA V2.0\nDEFAULT: N/A\nCol1\n0 A";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("N/A", result.GetValue(0, "NonExistent"));
    }

    [Fact]
    public void GetValue_EmptyCell_ReturnsDefault()
    {
        var content = "2DA V2.0\nDEFAULT: empty\nCol1 Col2\n0 **** B";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("empty", result.GetValue(0, "Col1"));
        Assert.Equal("B", result.GetValue(0, "Col2"));
    }

    [Fact]
    public void HasColumn_ExistingColumn_ReturnsTrue()
    {
        var content = "2DA V2.0\nName Type\n0 A B";

        var result = TwoDAReader.Parse(content);

        Assert.True(result.HasColumn("Name"));
        Assert.True(result.HasColumn("name")); // Case insensitive
        Assert.False(result.HasColumn("NonExistent"));
    }

    [Fact]
    public void GetColumnIndex_ExistingColumn_ReturnsIndex()
    {
        var content = "2DA V2.0\nFirst Second Third\n0 A B C";

        var result = TwoDAReader.Parse(content);

        Assert.Equal(0, result.GetColumnIndex("First"));
        Assert.Equal(1, result.GetColumnIndex("Second"));
        Assert.Equal(2, result.GetColumnIndex("Third"));
        Assert.Equal(-1, result.GetColumnIndex("Fourth"));
    }

    [Fact]
    public void Parse_InvalidSignature_ThrowsException()
    {
        var content = "INVALID\nCol1\n0 A";

        var ex = Assert.Throws<InvalidDataException>(() => TwoDAReader.Parse(content));
        Assert.Contains("Invalid 2DA signature", ex.Message);
    }

    [Fact]
    public void Parse_EmptyFile_ThrowsException()
    {
        var content = "";

        var ex = Assert.Throws<InvalidDataException>(() => TwoDAReader.Parse(content));
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Parse_OnlySignature_ReturnsEmptyTwoDA()
    {
        var content = "2DA V2.0";

        var result = TwoDAReader.Parse(content);

        Assert.Equal(0, result.ColumnCount);
        Assert.Equal(0, result.RowCount);
    }

    [Fact]
    public void RowIndexer_ReturnsValue()
    {
        var row = new TwoDARow { Values = new List<string?> { "A", "B", "C" } };

        Assert.Equal("A", row[0]);
        Assert.Equal("B", row[1]);
        Assert.Equal("C", row[2]);
        Assert.Null(row[99]); // Out of range
    }

    [Fact]
    public void Parse_EscapedQuotes_ParsesCorrectly()
    {
        var content = "2DA V2.0\nCol1\n0 \"He said \"\"Hello\"\"\"";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("He said \"Hello\"", result.Rows[0].Values[0]);
    }

    [Fact]
    public void Parse_NumericValues_ParsedAsStrings()
    {
        var content = "2DA V2.0\nID Value Cost\n0 100 3.14 1000";

        var result = TwoDAReader.Parse(content);

        Assert.Equal("100", result.Rows[0].Values[0]);
        Assert.Equal("3.14", result.Rows[0].Values[1]);
        Assert.Equal("1000", result.Rows[0].Values[2]);
    }

    [Fact]
    public void Parse_LeadingWhitespace_Trimmed()
    {
        var content = "   2DA V2.0\n   Col1 Col2\n   0 A B";

        var result = TwoDAReader.Parse(content);

        Assert.Equal(2, result.ColumnCount);
        Assert.Single(result.Rows);
    }
}
