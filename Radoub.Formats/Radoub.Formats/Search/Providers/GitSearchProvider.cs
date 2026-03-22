using System.Text.RegularExpressions;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for GIT (Game Instance) files.
/// Works at raw GFF level since no typed GIT model exists.
/// Walks all placed instance lists and searches string/locstring fields.
/// </summary>
public class GitSearchProvider : SearchProviderBase, IFileSearchProvider
{
    /// <summary>
    /// Known instance list labels mapped to human-readable type names.
    /// </summary>
    private static readonly (string ListLabel, string TypeName)[] InstanceLists =
    {
        ("Creature List", "Creature"),
        ("Door List", "Door"),
        ("Encounter List", "Encounter"),
        ("Placeable List", "Placeable"),
        ("SoundList", "Sound"),
        ("StoreList", "Store"),
        ("TriggerList", "Trigger"),
        ("WaypointList", "Waypoint"),
    };

    public ushort FileType => ResourceTypes.Git;

    public IReadOnlyList<string> Extensions => new[] { ".git" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        foreach (var (listLabel, typeName) in InstanceLists)
        {
            var listField = gffFile.RootStruct.Fields?.FirstOrDefault(f => f.Label == listLabel);
            if (listField?.Value is not GffList list) continue;

            for (int i = 0; i < list.Elements.Count; i++)
            {
                var instance = list.Elements[i];
                var tag = GetStringField(instance, "Tag") ?? $"#{i}";
                var location = new GitMatchLocation
                {
                    InstanceType = typeName,
                    InstanceIndex = i,
                    InstanceTag = tag,
                    DisplayPath = $"{typeName} #{i} ({tag})"
                };

                SearchInstance(instance, location, regex, matches);
            }
        }

        return matches;
    }

    private static void SearchInstance(GffStruct instance, GitMatchLocation location, Regex regex, List<SearchMatch> matches)
    {
        if (instance.Fields == null) return;

        foreach (var field in instance.Fields)
        {
            switch (field.Type)
            {
                case GffField.CExoString:
                    if (field.Value is string strValue && !string.IsNullOrEmpty(strValue))
                    {
                        var fieldDef = MakeFieldDef(field.Label, SearchFieldType.Text);
                        matches.AddRange(SearchString(strValue, fieldDef, regex, location));
                    }
                    break;

                case GffField.CResRef:
                    if (field.Value is string resRefValue && !string.IsNullOrEmpty(resRefValue))
                    {
                        var fieldDef = MakeFieldDef(field.Label, SearchFieldType.ResRef);
                        matches.AddRange(SearchString(resRefValue, fieldDef, regex, location));
                    }
                    break;

                case GffField.CExoLocString:
                    if (field.Value is CExoLocString locString)
                    {
                        var fieldDef = MakeFieldDef(field.Label, SearchFieldType.LocString);
                        matches.AddRange(SearchLocString(locString, fieldDef, regex, location));
                    }
                    break;

                case GffField.List when field.Label == "VarTable":
                    var varFieldDef = MakeFieldDef("VarTable", SearchFieldType.Variable);
                    matches.AddRange(SearchVarTable(instance, varFieldDef, regex, location));
                    break;
            }
        }
    }

    private static FieldDefinition MakeFieldDef(string fieldLabel, SearchFieldType fieldType)
    {
        var category = fieldType switch
        {
            SearchFieldType.LocString => SearchFieldCategory.Content,
            SearchFieldType.ResRef => SearchFieldCategory.Identity,
            SearchFieldType.Variable => SearchFieldCategory.Variable,
            _ when fieldLabel is "Tag" or "LinkedTo" => SearchFieldCategory.Identity,
            _ when fieldLabel.StartsWith("Script") || fieldLabel.StartsWith("On") => SearchFieldCategory.Script,
            _ => SearchFieldCategory.Metadata
        };

        return new FieldDefinition
        {
            Name = fieldLabel,
            GffPath = fieldLabel,
            FieldType = fieldType,
            Category = category,
            Description = $"GIT instance field: {fieldLabel}"
        };
    }

    private static string? GetStringField(GffStruct gffStruct, string label)
    {
        var field = gffStruct.Fields?.FirstOrDefault(f => f.Label == label);
        return field?.Value as string;
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        // Phase 3
        return operations.Select(op => new ReplaceResult
        {
            Success = false, Field = op.Match.Field,
            OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
            Skipped = true, SkipReason = "Replace not yet implemented for GIT provider"
        }).ToList();
    }
}

/// <summary>
/// Location within a GIT file — identifies the instance type, index, and tag.
/// </summary>
public class GitMatchLocation
{
    public required string InstanceType { get; init; }
    public required int InstanceIndex { get; init; }
    public string? InstanceTag { get; init; }
    public required string DisplayPath { get; init; }
}
