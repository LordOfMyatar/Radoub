namespace Radoub.UI.Controls;

/// <summary>
/// Sort direction for file browser panels. Repeated clicks on the same column
/// header toggle between Ascending and Descending; switching to a different
/// column resets to Ascending. Module-first tier and null-last placement are
/// preserved regardless of direction (#2200).
/// </summary>
public enum BrowserSortDirection
{
    Ascending,
    Descending
}
