using Radoub.Formats.Common;
using Radoub.Formats.Fac;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for FAC (faction) files.
/// Searches faction names with readable location display (e.g., "Faction #2: Commoner").
/// </summary>
public class FacSearchProvider : SearchProviderBase, IFileSearchProvider
{
    private static readonly FieldDefinition FactionNameField = new() { Name = "Faction Name", GffPath = "FactionName", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Content, Description = "Faction name" };

    public ushort FileType => ResourceTypes.Fac;

    public IReadOnlyList<string> Extensions => new[] { ".fac" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var bytes = GffWriter.Write(gffFile);
        var fac = FacReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        if (!criteria.MatchesField(FactionNameField))
            return matches;

        for (int i = 0; i < fac.FactionList.Count; i++)
        {
            var faction = fac.FactionList[i];
            var location = new FacMatchLocation
            {
                FactionIndex = i,
                DisplayPath = $"Faction #{i}: {faction.FactionName}"
            };

            matches.AddRange(SearchString(faction.FactionName, FactionNameField, regex, location));
        }

        return matches;
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        if (operations.Count == 0) return Array.Empty<ReplaceResult>();

        var sorted = SortReverseOffset(operations);
        var results = new List<ReplaceResult>();

        foreach (var op in sorted)
        {
            if (op.Match.Location is not FacMatchLocation facLocation)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = "Invalid location for FAC replace"
                });
                continue;
            }

            // Navigate to FactionList[index].FactionName
            var factionListField = gffFile.RootStruct.GetField("FactionList");
            if (factionListField?.Value is not GffList factionList ||
                facLocation.FactionIndex >= factionList.Elements.Count)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = $"Faction index {facLocation.FactionIndex} not found"
                });
                continue;
            }

            var factionStruct = factionList.Elements[facLocation.FactionIndex];
            results.Add(ReplaceStringField(factionStruct, "FactionName", op));
        }

        return results;
    }
}

/// <summary>
/// Location within a FAC faction file.
/// </summary>
public class FacMatchLocation
{
    public required int FactionIndex { get; init; }
    public required string DisplayPath { get; init; }

    public override string ToString() => DisplayPath;
}
