namespace Radoub.UI.Services.Search;

/// <summary>
/// Progress reporting for module-wide search operations.
/// </summary>
public class ScanProgress
{
    /// <summary>Current phase of the scan (e.g., "Discovering files", "Searching")</summary>
    public string Phase { get; init; } = "";

    /// <summary>Name of the file currently being processed</summary>
    public string CurrentFile { get; init; } = "";

    /// <summary>Number of files scanned so far</summary>
    public int FilesScanned { get; init; }

    /// <summary>Total number of files to scan (0 if not yet known)</summary>
    public int TotalFiles { get; init; }

    /// <summary>Number of matches found so far</summary>
    public int MatchesFound { get; init; }
}
