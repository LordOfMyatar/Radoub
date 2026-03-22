using Radoub.Formats.Common;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search;

/// <summary>
/// Fallback search provider that walks any GFF file's fields.
/// Searches all CExoLocString, CExoString, and CResRef fields.
/// Used for file types without a dedicated provider.
/// </summary>
public class GenericGffSearchProvider : SearchProviderBase, IFileSearchProvider
{
    public ushort FileType => ResourceTypes.Gff;

    public IReadOnlyList<string> Extensions => new[] { ".gff" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        // Full implementation in #1839
        throw new NotImplementedException("GenericGffSearchProvider.Search will be implemented in #1839");
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        // Replace implementation deferred to Phase 3
        throw new NotImplementedException("Replace is Phase 3");
    }
}
