namespace Radoub.UI.Services.Search;

/// <summary>
/// Aggregated search results from a module-wide scan.
/// </summary>
public class ModuleSearchResults
{
    /// <summary>Per-file results (only files with matches or errors)</summary>
    public required IReadOnlyList<FileSearchResult> Files { get; init; }

    /// <summary>Total matches across all files</summary>
    public int TotalMatches => Files.Sum(f => f.MatchCount);

    /// <summary>Number of files that had at least one match</summary>
    public int FilesWithMatches => Files.Count(f => f.MatchCount > 0);

    /// <summary>Total number of files scanned (including those with no matches)</summary>
    public int TotalFilesScanned { get; init; }

    /// <summary>Number of files that failed to parse</summary>
    public int ParseErrors => Files.Count(f => f.HadParseError);

    /// <summary>True if the scan was cancelled before completion</summary>
    public bool WasCancelled { get; init; }

    /// <summary>How long the scan took</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Group results by file extension</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<FileSearchResult>> GroupByExtension()
    {
        return Files
            .Where(f => f.MatchCount > 0)
            .GroupBy(f => f.Extension)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<FileSearchResult>)g.ToList());
    }

    /// <summary>Group results by tool identifier</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<FileSearchResult>> GroupByTool()
    {
        return Files
            .Where(f => f.MatchCount > 0)
            .GroupBy(f => f.ToolId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<FileSearchResult>)g.ToList());
    }
}
