using Radoub.Formats.Common;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for dialog (.dlg) files.
/// Walks the dialog tree (entries, replies, starting links) and searches
/// text, speaker, scripts, parameters, and metadata fields.
/// </summary>
public class DlgSearchProvider : SearchProviderBase, IFileSearchProvider
{
    public ushort FileType => ResourceTypes.Dlg;

    public IReadOnlyList<string> Extensions => new[] { ".dlg" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        // Full implementation in #1840
        throw new NotImplementedException("DlgSearchProvider.Search will be implemented in #1840");
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        // Replace implementation deferred to Phase 3
        throw new NotImplementedException("Replace is Phase 3");
    }
}
