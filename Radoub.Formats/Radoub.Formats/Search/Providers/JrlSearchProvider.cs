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
            // Use category tag or name for display (tag is more reliable, name may be localized)
            var catName = category.Name?.LocalizedStrings.Values.FirstOrDefault();
            var catLabel = !string.IsNullOrEmpty(category.Tag) ? category.Tag
                : !string.IsNullOrEmpty(catName) ? catName
                : $"Category #{catIdx}";
            var catDisplayPath = catLabel;
            var catLocation = new JrlMatchLocation
            {
                CategoryIndex = catIdx,
                DisplayPath = catDisplayPath
            };

            if (criteria.MatchesField(CategoryNameField))
                matches.AddRange(SearchLocString(category.Name, CategoryNameField, regex, catLocation, criteria.EffectiveTlkResolver));
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
                    DisplayPath = $"{catLabel} → Entry {entry.ID}"
                };

                if (criteria.MatchesField(EntryTextField))
                    matches.AddRange(SearchLocString(entry.Text, EntryTextField, regex, entryLocation, criteria.EffectiveTlkResolver));
            }
        }

        return matches;
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        if (operations.Count == 0) return Array.Empty<ReplaceResult>();

        var sorted = SortReverseOffset(operations);
        var results = new List<ReplaceResult>();

        // Get Categories list from root
        var categoriesField = gffFile.RootStruct.GetField("Categories");
        if (categoriesField?.Value is not GffList categoriesList)
        {
            return operations.Select(op => new ReplaceResult
            {
                Success = false, Field = op.Match.Field,
                OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                Skipped = true, SkipReason = "Categories list not found in GFF"
            }).ToList();
        }

        foreach (var op in sorted)
        {
            if (op.Match.Location is not JrlMatchLocation loc)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = "Missing JRL location info"
                });
                continue;
            }

            if (loc.CategoryIndex < 0 || loc.CategoryIndex >= categoriesList.Elements.Count)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = $"Category index {loc.CategoryIndex} out of range"
                });
                continue;
            }

            var categoryStruct = categoriesList.Elements[loc.CategoryIndex];

            if (loc.EntryId != null)
            {
                // Target is an entry within the category
                var entryStruct = FindEntryStruct(categoryStruct, loc.EntryId.Value);
                if (entryStruct == null)
                {
                    results.Add(new ReplaceResult
                    {
                        Success = false, Field = op.Match.Field,
                        OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                        Skipped = true, SkipReason = $"Entry ID {loc.EntryId} not found in category"
                    });
                    continue;
                }

                var result = op.Match.Field.FieldType switch
                {
                    SearchFieldType.LocString => ReplaceLocStringField(entryStruct, op.Match.Field.GffPath, op),
                    _ => ReplaceStringField(entryStruct, op.Match.Field.GffPath, op)
                };
                results.Add(result);
            }
            else
            {
                // Target is on the category itself
                var result = op.Match.Field.FieldType switch
                {
                    SearchFieldType.LocString => ReplaceLocStringField(categoryStruct, op.Match.Field.GffPath, op),
                    _ => ReplaceStringField(categoryStruct, op.Match.Field.GffPath, op)
                };
                results.Add(result);
            }
        }

        return results;
    }

    private static GffStruct? FindEntryStruct(GffStruct categoryStruct, uint entryId)
    {
        var entryListField = categoryStruct.GetField("EntryList");
        if (entryListField?.Value is not GffList entryList) return null;

        foreach (var entryStruct in entryList.Elements)
        {
            var idField = entryStruct.GetField("ID");
            if (idField != null)
            {
                var id = Convert.ToUInt32(idField.Value);
                if (id == entryId) return entryStruct;
            }
        }

        return null;
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

    public override string ToString() => DisplayPath;
}
