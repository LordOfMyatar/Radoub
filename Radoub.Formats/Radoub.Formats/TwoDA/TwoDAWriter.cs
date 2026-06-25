using System.Text;

namespace Radoub.Formats.TwoDA;

/// <summary>
/// Writes 2DA (Two-Dimensional Array) files to text format.
/// Formatting matches neverwinter.nim twoda.nim writeTwoDA: "2DA V2.0" header,
/// an optional DEFAULT line, column-aligned text rows (width = max of header and
/// cell widths per column), "****" for null cells, CP-1252 encoding, and row
/// indices in the leftmost unnamed column. Reference:
/// https://github.com/niv/neverwinter.nim/blob/master/neverwinter/twoda.nim
/// </summary>
public static class TwoDAWriter
{
    private const string Header = "2DA V2.0";
    private const string EmptyCell = "****";
    private const string Newline = "\r\n";
    private const int CellPadding = 2;

    private static readonly Encoding NwnEncoding;

    static TwoDAWriter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        NwnEncoding = Encoding.GetEncoding(1252);
    }

    /// <summary>
    /// Write a 2DA file to a byte array (CP-1252 encoded).
    /// </summary>
    public static byte[] Write(TwoDAFile twoDA)
    {
        return NwnEncoding.GetBytes(WriteString(twoDA));
    }

    /// <summary>
    /// Write a 2DA file to a stream (CP-1252 encoded).
    /// </summary>
    public static void Write(TwoDAFile twoDA, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var bytes = Write(twoDA);
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Write a 2DA file to a file path (CP-1252 encoded).
    /// </summary>
    public static void Write(TwoDAFile twoDA, string filePath)
    {
        File.WriteAllBytes(filePath, Write(twoDA));
    }

    /// <summary>
    /// Render a 2DA file to its text form. The bytes returned by <see cref="Write(TwoDAFile)"/>
    /// are this string encoded as CP-1252.
    /// </summary>
    public static string WriteString(TwoDAFile twoDA)
    {
        ArgumentNullException.ThrowIfNull(twoDA);

        if (twoDA.Columns.Count == 0)
            throw new InvalidOperationException("Cannot write a 2DA with no columns configured.");

        int columnCount = twoDA.Columns.Count;

        // maxColWidth[idx] = max(header length, longest non-null cell in that column).
        var maxColWidth = new int[columnCount];
        for (int idx = 0; idx < columnCount; idx++)
        {
            int width = twoDA.Columns[idx].Length;
            foreach (var row in twoDA.Rows)
            {
                if (idx < row.Values.Count && row.Values[idx] != null)
                    width = Math.Max(width, row.Values[idx]!.Length);
            }
            maxColWidth[idx] = width;
        }

        // idWidth keyed to the row count (matching neverwinter.nim's $self.rows.len).
        int idWidth = Math.Max(3, twoDA.Rows.Count.ToString().Length);

        var sb = new StringBuilder();

        // Header
        sb.Append(Header).Append(Newline);

        // DEFAULT line (always emit the line; content only if a default is set).
        if (twoDA.DefaultValue != null)
            sb.Append("DEFAULT: ").Append(EscapeField(twoDA.DefaultValue));
        sb.Append(Newline);

        // Column header row: leading indent for the unnamed id column, then headers.
        sb.Append(new string(' ', idWidth + CellPadding));
        for (int idx = 0; idx < columnCount; idx++)
        {
            var h = twoDA.Columns[idx];
            sb.Append(h);
            if (idx != columnCount - 1)
                sb.Append(new string(' ', maxColWidth[idx] - h.Length + 3 + CellPadding));
        }
        sb.Append(Newline);

        // Data rows. The leftmost column is the numeric row index (the stored Label
        // is intentionally ignored — the engine does not use it, per twoda.nim).
        for (int rowIdx = 0; rowIdx < twoDA.Rows.Count; rowIdx++)
        {
            var row = twoDA.Rows[rowIdx];
            string thisId = rowIdx.ToString();
            sb.Append(thisId).Append(new string(' ', idWidth + CellPadding - thisId.Length));

            for (int cellIdx = 0; cellIdx < columnCount; cellIdx++)
            {
                string? cell = cellIdx < row.Values.Count ? row.Values[cellIdx] : null;
                string fmt = EscapeField(cell);
                sb.Append(fmt);
                if (cellIdx != columnCount - 1)
                    sb.Append(new string(' ', maxColWidth[cellIdx] - fmt.Length + 3 + CellPadding));
            }
            sb.Append(Newline);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Transforms a cell value for 2DA writing. Null becomes "****"; empty or
    /// whitespace-containing values are double-quoted. Embedded double-quotes are
    /// not representable in the 2DA format and raise an error (matches twoda.nim).
    /// </summary>
    private static string EscapeField(string? field)
    {
        if (field == null)
            return EmptyCell;

        if (field.IndexOf('"') != -1)
            throw new InvalidOperationException("Cannot write a 2DA cell containing a double-quote character.");

        if (field.Length == 0 || ContainsWhitespace(field))
            return "\"" + field + "\"";

        return field;
    }

    private static bool ContainsWhitespace(string value)
    {
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
                return true;
        }
        return false;
    }
}
