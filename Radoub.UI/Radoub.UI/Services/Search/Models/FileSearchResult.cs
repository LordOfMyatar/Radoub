using Radoub.Formats.Search;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Search results for a single file within a module scan.
/// </summary>
public class FileSearchResult
{
    /// <summary>Full path to the file that was searched</summary>
    public required string FilePath { get; init; }

    /// <summary>Filename without path (e.g., "merchant_01.dlg")</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>File extension without dot, lowercase (e.g., "dlg")</summary>
    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();

    /// <summary>Resource type constant (e.g., ResourceTypes.Dlg = 2029)</summary>
    public ushort ResourceType { get; init; }

    /// <summary>
    /// Tool identifier for launching the appropriate Radoub editor.
    /// e.g., "parley" for .dlg, "quartermaster" for .utc/.bic, "relique" for .uti
    /// </summary>
    public string ToolId { get; init; } = "";

    /// <summary>All matches found in this file</summary>
    public required IReadOnlyList<SearchMatch> Matches { get; init; }

    /// <summary>Number of matches in this file</summary>
    public int MatchCount => Matches.Count;

    /// <summary>True if the file had parse errors (searched via fallback or skipped)</summary>
    public bool HadParseError { get; init; }

    /// <summary>Error message if parsing failed</summary>
    public string? ParseError { get; init; }
}
