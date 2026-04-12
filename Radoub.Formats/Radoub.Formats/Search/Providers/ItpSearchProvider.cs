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
        // ITP palette files: names are replaceable but ResRefs are not.
        // For now, delegate to GenericGffSearchProvider for replace operations
        // since palette editing is rare and the tree structure makes targeted replacement complex.
        if (operations.Count == 0) return Array.Empty<ReplaceResult>();

        return operations.Select(op => new ReplaceResult
        {
            Success = false,
            Field = op.Match.Field,
            OldValue = op.Match.FullFieldValue,
            NewValue = op.ReplacementText,
            Skipped = true,
            SkipReason = "ITP palette replace not yet supported"
        }).ToList();
    }
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
