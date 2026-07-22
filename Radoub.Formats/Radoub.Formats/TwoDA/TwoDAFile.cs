namespace Radoub.Formats.TwoDA;

/// <summary>
/// Represents a 2DA (Two-Dimensional Array) file used for game data tables in Aurora Engine.
/// Reference: neverwinter.nim twoda.nim
/// </summary>
public class TwoDAFile
{
    /// <summary>
    /// File signature - "2DA"
    /// </summary>
    public string FileType { get; set; } = "2DA";

    /// <summary>
    /// File version - "V2.0"
    /// </summary>
    public string FileVersion { get; set; } = "V2.0";

    /// <summary>
    /// Default value for missing cells. May be null if not specified.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Column headers.
    /// </summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>
    /// Row data. Each row contains cell values indexed by column position.
    /// Empty cells (****) are stored as null.
    /// </summary>
    public List<TwoDARow> Rows { get; set; } = new();

    /// <summary>
    /// Get a cell value by row index and column name.
    /// Returns null if the cell is empty or out of range.
    /// </summary>
    public string? GetValue(int rowIndex, string columnName)
    {
        if (rowIndex < 0 || rowIndex >= Rows.Count)
            return DefaultValue;

        var colIndex = GetColumnIndex(columnName);
        if (colIndex < 0)
            return DefaultValue;

        return Rows[rowIndex].GetValue(colIndex) ?? DefaultValue;
    }

    /// <summary>
    /// Get a cell value by row index and column index.
    /// Returns null if the cell is empty or out of range.
    /// </summary>
    public string? GetValue(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= Rows.Count)
            return DefaultValue;

        if (columnIndex < 0 || columnIndex >= Columns.Count)
            return DefaultValue;

        return Rows[rowIndex].GetValue(columnIndex) ?? DefaultValue;
    }

    // Lazily-built name -> index map. GetColumnIndex is called once per GetValue-by-name, so a
    // per-file cache turns the hot path from an O(columns) scan into a dictionary hit (#2580).
    // Rebuilt whenever Columns.Count changes, covering parsers that reuse an instance. A rename
    // that keeps the same count would not invalidate, but 2DA columns are built once and never
    // renamed in place, so that case does not arise.
    private Dictionary<string, int>? _columnIndexCache;
    private int _columnIndexCacheCount = -1;

    /// <summary>
    /// Get the column index for a column name (case-insensitive).
    /// Returns -1 if not found.
    /// </summary>
    public int GetColumnIndex(string columnName)
    {
        if (_columnIndexCache is null || _columnIndexCacheCount != Columns.Count)
        {
            var cache = new Dictionary<string, int>(Columns.Count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Columns.Count; i++)
                cache.TryAdd(Columns[i], i); // first occurrence wins, matching the old linear scan
            _columnIndexCache = cache;
            _columnIndexCacheCount = Columns.Count;
        }

        return _columnIndexCache.TryGetValue(columnName, out int index) ? index : -1;
    }

    /// <summary>
    /// Check if a column exists.
    /// </summary>
    public bool HasColumn(string columnName) => GetColumnIndex(columnName) >= 0;

    /// <summary>
    /// Number of rows.
    /// </summary>
    public int RowCount => Rows.Count;

    /// <summary>
    /// Number of columns.
    /// </summary>
    public int ColumnCount => Columns.Count;
}

/// <summary>
/// A single row in a 2DA file.
/// </summary>
public class TwoDARow
{
    /// <summary>
    /// Row label/index from the file (usually matches array position).
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Cell values. Null represents empty cells (****).
    /// </summary>
    public List<string?> Values { get; set; } = new();

    /// <summary>
    /// Get a cell value by column index.
    /// Returns null if index out of range or cell is empty.
    /// </summary>
    public string? GetValue(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= Values.Count)
            return null;
        return Values[columnIndex];
    }

    /// <summary>
    /// Indexer for cell values.
    /// </summary>
    public string? this[int columnIndex] => GetValue(columnIndex);
}
