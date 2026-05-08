using Radoub.Formats.Common;
using Radoub.Formats.Search;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Module-scoped search provider that finds files whose ResRef portion
/// (filename minus extension) matches the search pattern. Unlike per-file
/// providers (IFileSearchProvider), this scans the module directory directly.
/// Honors SearchCriteria.FileTypeFilter — files of unchecked types are skipped.
/// See spec Section 3 + Section 5 (NonPublic/Trebuchet/2026-05-03-resref-rename-design.md).
/// </summary>
public class FilenameSearchProvider
{
    /// <summary>Same set of searchable extensions ModuleSearchService uses, plus .nss for rename mode.</summary>
    private static readonly HashSet<string> SearchableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dlg", ".utc", ".bic", ".uti", ".utm", ".jrl", ".ifo", ".fac",
        ".are", ".git", ".utp", ".ute", ".utt", ".utw", ".utd", ".uts", ".itp",
        ".nss"  // NSS added for filename-rename mode (existing search ignores .nss; rename includes it)
    };

    /// <summary>Virtual FieldDefinition representing "the file's own name".
    /// IsReplaceable=false here; the BatchReplaceService bypass widens to ResRef-type fields.</summary>
    public static readonly FieldDefinition FilenameField = new()
    {
        Name = "Filename",
        GffPath = "<filename>",
        FieldType = SearchFieldType.ResRef,
        Category = SearchFieldCategory.Identity,
        Description = "Module file name (ResRef portion)",
        IsReplaceable = false  // overridden by allowResRefReplace bypass in BatchReplaceService
    };

    public IReadOnlyList<FileSearchResult> Search(string moduleDir, SearchCriteria criteria)
    {
        if (string.IsNullOrEmpty(moduleDir) || !Directory.Exists(moduleDir))
            return Array.Empty<FileSearchResult>();

        var validationError = criteria.Validate();
        if (validationError != null)
            return Array.Empty<FileSearchResult>();

        var pattern = criteria.ToRegex();
        var results = new List<FileSearchResult>();

        foreach (var filePath in Directory.EnumerateFiles(moduleDir))
        {
            var ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext) || !SearchableExtensions.Contains(ext))
                continue;

            var resourceType = ResourceTypes.FromExtension(ext);

            // Honor file-type filter — unchecked types are skipped per spec scope-respecting rule
            if (criteria.FileTypeFilter != null && criteria.FileTypeFilter.Count > 0
                && !criteria.FileTypeFilter.Contains(resourceType))
                continue;

            var resRefPortion = Path.GetFileNameWithoutExtension(filePath);
            var match = pattern.Match(resRefPortion);
            if (!match.Success) continue;

            var searchMatch = new SearchMatch
            {
                Field = FilenameField,
                MatchedText = match.Value,
                FullFieldValue = resRefPortion,
                MatchOffset = match.Index,
                MatchLength = match.Length,
                Location = $"<filename: {Path.GetFileName(filePath)}>"
            };

            results.Add(new FileSearchResult
            {
                FilePath = filePath,
                ResourceType = resourceType,
                ToolId = ModuleSearchService.GetToolId(resourceType),
                Matches = new[] { searchMatch }
            });
        }

        return results;
    }
}
