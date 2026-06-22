using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Itp;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for ITP (palette) files.
/// Walks the palette tree and searches branch names, category names, and blueprint ResRefs/names.
/// Provides human-readable hierarchical display paths (e.g., "Armor → Medium → King Snake Robe").
/// </summary>
public class ItpSearchProvider : SearchProviderBase, IFileSearchProvider
{
    private static readonly FieldDefinition BranchNameField = new() { Name = "Branch Name", GffPath = "NAME", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Content, Description = "Palette branch name" };
    private static readonly FieldDefinition CategoryNameField = new() { Name = "Category Name", GffPath = "NAME", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Content, Description = "Palette category name" };
    private static readonly FieldDefinition BlueprintNameField = new() { Name = "Name", GffPath = "NAME", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Content, Description = "Blueprint name" };
    private static readonly FieldDefinition ResRefField = new() { Name = "ResRef", GffPath = "RESREF", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Blueprint resource reference", IsReplaceable = false };

    public ushort FileType => ResourceTypes.Itp;

    public IReadOnlyList<string> Extensions => new[] { ".itp" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var itp = ItpReader.Read(gffFile);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        SearchNodes(itp.MainNodes, criteria, regex, matches, new List<string>());

        return matches;
    }

    private void SearchNodes(List<PaletteNode> nodes, SearchCriteria criteria,
        System.Text.RegularExpressions.Regex regex, List<SearchMatch> matches, List<string> pathParts)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case PaletteBlueprintNode blueprint:
                    SearchBlueprint(blueprint, criteria, regex, matches, pathParts);
                    break;
                case PaletteCategoryNode category:
                    SearchCategory(category, criteria, regex, matches, pathParts);
                    break;
                case PaletteBranchNode branch:
                    SearchBranch(branch, criteria, regex, matches, pathParts);
                    break;
            }
        }
    }

    private void SearchBranch(PaletteBranchNode branch, SearchCriteria criteria,
        System.Text.RegularExpressions.Regex regex, List<SearchMatch> matches, List<string> pathParts)
    {
        var branchName = branch.Name ?? branch.DeleteMe ?? $"StrRef:{branch.StrRef}";

        var location = new ItpMatchLocation
        {
            NodeType = ItpNodeType.Branch,
            DisplayPath = BuildPath(pathParts, branchName)
        };

        if (criteria.MatchesField(BranchNameField) && !string.IsNullOrEmpty(branch.Name))
            matches.AddRange(SearchString(branch.Name, BranchNameField, regex, location));

        pathParts.Add(branchName);
        SearchNodes(branch.Children, criteria, regex, matches, pathParts);
        pathParts.RemoveAt(pathParts.Count - 1);
    }

    private void SearchCategory(PaletteCategoryNode category, SearchCriteria criteria,
        System.Text.RegularExpressions.Regex regex, List<SearchMatch> matches, List<string> pathParts)
    {
        var categoryName = category.Name ?? category.DeleteMe ?? $"StrRef:{category.StrRef}";

        var location = new ItpMatchLocation
        {
            NodeType = ItpNodeType.Category,
            CategoryId = category.Id,
            DisplayPath = BuildPath(pathParts, categoryName)
        };

        if (criteria.MatchesField(CategoryNameField) && !string.IsNullOrEmpty(category.Name))
            matches.AddRange(SearchString(category.Name, CategoryNameField, regex, location));

        pathParts.Add(categoryName);
        foreach (var blueprint in category.Blueprints)
        {
            SearchBlueprint(blueprint, criteria, regex, matches, pathParts);
        }
        // Recurse into nested categories/branches (#2280 reader, #2475 search).
        SearchNodes(category.Children, criteria, regex, matches, pathParts);
        pathParts.RemoveAt(pathParts.Count - 1);
    }

    private void SearchBlueprint(PaletteBlueprintNode blueprint, SearchCriteria criteria,
        System.Text.RegularExpressions.Regex regex, List<SearchMatch> matches, List<string> pathParts)
    {
        var blueprintName = blueprint.Name ?? blueprint.DeleteMe ?? blueprint.ResRef;

        var location = new ItpMatchLocation
        {
            NodeType = ItpNodeType.Blueprint,
            DisplayPath = BuildPath(pathParts, blueprintName)
        };

        if (criteria.MatchesField(ResRefField))
            matches.AddRange(SearchString(blueprint.ResRef, ResRefField, regex, location));
        if (criteria.MatchesField(BlueprintNameField) && !string.IsNullOrEmpty(blueprint.Name))
            matches.AddRange(SearchString(blueprint.Name, BlueprintNameField, regex, location));
    }

    private static string BuildPath(List<string> pathParts, string current)
    {
        if (pathParts.Count == 0) return current;
        return string.Join(" \u2192 ", pathParts) + " \u2192 " + current;
    }

    public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations)
    {
        // ITP palette replace (#2178 follow-up): walk the MAIN tree, find every
        // blueprint struct whose target field (RESREF or NAME) equals the
        // operation's FullFieldValue, and apply the substring replacement at
        // MatchOffset/MatchLength.
        if (operations.Count == 0) return Array.Empty<ReplaceResult>();

        var mainField = gffFile.RootStruct.GetField("MAIN");
        if (mainField?.Value is not GffList mainList)
        {
            return operations.Select(op => MakeSkipped(op, "ITP has no MAIN list")).ToList();
        }

        var results = new List<ReplaceResult>();
        foreach (var op in operations)
        {
            var fieldName = op.Match.Field.GffPath;  // "RESREF" or "NAME"
            if (string.IsNullOrEmpty(fieldName))
            {
                results.Add(MakeSkipped(op, "Missing field path"));
                continue;
            }

            bool isResRefField = op.Match.Field.FieldType == SearchFieldType.ResRef;
            if (isResRefField && !op.AllowResRefReplace)
            {
                results.Add(MakeSkipped(op, "ResRef field requires allowResRefReplace"));
                continue;
            }

            int updated = ReplaceInTree(mainList, fieldName, op);
            if (updated > 0)
            {
                results.Add(new ReplaceResult
                {
                    Success = true,
                    Field = op.Match.Field,
                    OldValue = op.Match.FullFieldValue,
                    NewValue = ComputeNewValue(op.Match.FullFieldValue, op.Match.MatchOffset, op.Match.MatchLength, op.ReplacementText)
                });
            }
            else
            {
                results.Add(MakeSkipped(op, "No matching blueprint node found"));
            }
        }
        return results;
    }

    private static int ReplaceInTree(GffList list, string fieldName, ReplaceOperation op)
    {
        int updated = 0;
        foreach (var node in list.Elements)
        {
            var f = node.GetField(fieldName);
            if (f?.Value is string current
                && string.Equals(current, op.Match.FullFieldValue, StringComparison.Ordinal))
            {
                f.Value = ComputeNewValue(current, op.Match.MatchOffset, op.Match.MatchLength, op.ReplacementText);
                updated++;
            }

            // Recurse into nested LIST
            var childList = node.GetField("LIST")?.Value as GffList;
            if (childList != null)
                updated += ReplaceInTree(childList, fieldName, op);
        }
        return updated;
    }

    private static string ComputeNewValue(string original, int offset, int length, string replacement)
    {
        if (offset < 0 || offset > original.Length) return original;
        if (length < 0 || offset + length > original.Length) return original;
        return string.Concat(
            original.AsSpan(0, offset),
            replacement,
            original.AsSpan(offset + length));
    }

    private static ReplaceResult MakeSkipped(ReplaceOperation op, string reason) => new()
    {
        Success = false,
        Field = op.Match.Field,
        OldValue = op.Match.FullFieldValue,
        NewValue = op.ReplacementText,
        Skipped = true,
        SkipReason = reason
    };
}

/// <summary>
/// Location within an ITP palette tree.
/// </summary>
public class ItpMatchLocation
{
    public required ItpNodeType NodeType { get; init; }
    public byte? CategoryId { get; init; }
    public required string DisplayPath { get; init; }

    public override string ToString() => DisplayPath;
}

public enum ItpNodeType
{
    Branch,
    Category,
    Blueprint
}
