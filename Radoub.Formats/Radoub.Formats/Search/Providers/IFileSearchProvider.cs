using Radoub.Formats.Gff;

namespace Radoub.Formats.Search;

/// <summary>
/// Searches and replaces within a single parsed GFF file.
/// Implementations are stateless and thread-safe.
/// </summary>
public interface IFileSearchProvider
{
    /// <summary>Resource type this provider handles (e.g., ResourceTypes.Dlg)</summary>
    ushort FileType { get; }

    /// <summary>File extensions this provider handles (e.g., ".dlg")</summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Search a parsed GFF file for matches.
    /// </summary>
    IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria);

    /// <summary>
    /// Apply replace operations to a parsed GFF file.
    /// Operations targeting the same field are applied in reverse offset order.
    /// Returns results in same order as input operations.
    /// </summary>
    IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations);
}
