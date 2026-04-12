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

                SearchInstance(instance, location, regex, matches, criteria.EffectiveTlkResolver);
            }
        }

        return matches;
    }

    private static void SearchInstance(GffStruct instance, GitMatchLocation location, Regex regex, List<SearchMatch> matches, Func<uint, string?>? tlkResolver)
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
                        matches.AddRange(SearchLocString(locString, fieldDef, regex, location, tlkResolver));
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
        if (operations.Count == 0) return Array.Empty<ReplaceResult>();

        var sorted = SortReverseOffset(operations);
        var results = new List<ReplaceResult>();

        foreach (var op in sorted)
        {
            if (op.Match.Location is not GitMatchLocation loc)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = "Missing GIT location info"
                });
                continue;
            }

            // Find the instance struct
            var instanceStruct = FindInstanceStruct(gffFile.RootStruct, loc);
            if (instanceStruct == null)
            {
                results.Add(new ReplaceResult
                {
                    Success = false, Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                    Skipped = true, SkipReason = $"Instance not found: {loc.DisplayPath}"
                });
                continue;
            }

            var result = op.Match.Field.FieldType switch
            {
                SearchFieldType.LocString => ReplaceLocStringField(instanceStruct, op.Match.Field.GffPath, op),
                SearchFieldType.Variable => ReplaceVarTableField(instanceStruct, op),
                _ => ReplaceStringField(instanceStruct, op.Match.Field.GffPath, op)
            };
            results.Add(result);
        }

        return results;
    }

    private static GffStruct? FindInstanceStruct(GffStruct root, GitMatchLocation loc)
    {
        // Map type name back to list label
        var listLabel = InstanceLists.FirstOrDefault(il => il.TypeName == loc.InstanceType).ListLabel;
        if (listLabel == null) return null;

        var listField = root.Fields?.FirstOrDefault(f => f.Label == listLabel);
        if (listField?.Value is not GffList list) return null;

        if (loc.InstanceIndex < 0 || loc.InstanceIndex >= list.Elements.Count) return null;

        return list.Elements[loc.InstanceIndex];
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

    public override string ToString() => DisplayPath;
}
