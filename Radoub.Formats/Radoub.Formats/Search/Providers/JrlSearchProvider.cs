using System.Text.RegularExpressions;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Jrl;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for JRL (journal/quest) files.
/// Walks categories and entries with hierarchical location tracking.
/// </summary>
public class JrlSearchProvider : SearchProviderBase, IFileSearchProvider
{
    private static readonly FieldDefinition CategoryNameField = new() { Name = "Category Name", GffPath = "Name", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Quest/category name" };
    private static readonly FieldDefinition CategoryTagField = new() { Name = "Category Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Quest tag" };
    private static readonly FieldDefinition EntryTextField = new() { Name = "Entry Text", GffPath = "Text", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Journal entry text" };
    private static readonly FieldDefinition CommentField = new() { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" };

    public ushort FileType => ResourceTypes.Jrl;

    public IReadOnlyList<string> Extensions => new[] { ".jrl" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var bytes = GffWriter.Write(gffFile);
        var jrl = JrlReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        for (int catIdx = 0; catIdx < jrl.Categories.Count; catIdx++)
        {
            var category = jrl.Categories[catIdx];
            var catDisplayPath = $"Category #{catIdx}";
            var catLocation = new JrlMatchLocation
            {
                CategoryIndex = catIdx,
                DisplayPath = catDisplayPath
            };

            if (criteria.MatchesField(CategoryNameField))
                matches.AddRange(SearchLocString(category.Name, CategoryNameField, regex, catLocation));
            if (criteria.MatchesField(CategoryTagField))
                matches.AddRange(SearchString(category.Tag, CategoryTagField, regex, catLocation));
            if (criteria.MatchesField(CommentField))
                matches.AddRange(SearchString(category.Comment, CommentField, regex, catLocation));

            // Entries
            for (int entIdx = 0; entIdx < category.Entries.Count; entIdx++)
            {
                var entry = category.Entries[entIdx];
                var entryLocation = new JrlMatchLocation
                {
                    CategoryIndex = catIdx,
                    EntryId = entry.ID,
                    DisplayPath = $"{catDisplayPath} → Entry #{entry.ID}"
                };

                if (criteria.MatchesField(EntryTextField))
                    matches.AddRange(SearchLocString(entry.Text, EntryTextField, regex, entryLocation));
            }
        }

        return matches;
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        // Phase 3
        return operations.Select(op => new ReplaceResult
        {
            Success = false, Field = op.Match.Field,
            OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
            Skipped = true, SkipReason = "Replace not yet implemented for JRL provider"
        }).ToList();
    }
}

/// <summary>
/// Location within a JRL file hierarchy.
/// </summary>
public class JrlMatchLocation
{
    public required int CategoryIndex { get; init; }
    public uint? EntryId { get; init; }
    public required string DisplayPath { get; init; }
}
