using System.Text.RegularExpressions;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Utm;

namespace Radoub.Formats.Search;

/// <summary>
/// Search provider for UTM (store/merchant) files.
/// Searches name, tag, resref, comment, scripts, local variables, and inventory items.
/// Optionally resolves inventory item display names via a callback for name-based search.
/// </summary>
public class UtmSearchProvider : SearchProviderBase, IFileSearchProvider
{
    private static readonly FieldDefinition NameField = new() { Name = "Name", GffPath = "LocName", FieldType = SearchFieldType.LocString, Category = SearchFieldCategory.Content, Description = "Store name" };
    private static readonly FieldDefinition TagField = new() { Name = "Tag", GffPath = "Tag", FieldType = SearchFieldType.Tag, Category = SearchFieldCategory.Identity, Description = "Store tag" };
    private static readonly FieldDefinition ResRefField = new() { Name = "ResRef", GffPath = "ResRef", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Store resource reference", IsReplaceable = false };
    private static readonly FieldDefinition CommentField = new() { Name = "Comment", GffPath = "Comment", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Metadata, Description = "Toolset comment" };
    private static readonly FieldDefinition OnOpenStoreField = new() { Name = "OnOpenStore", GffPath = "OnOpenStore", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script when store opened" };
    private static readonly FieldDefinition OnStoreClosedField = new() { Name = "OnStoreClosed", GffPath = "OnStoreClosed", FieldType = SearchFieldType.Script, Category = SearchFieldCategory.Script, Description = "Script when store closed" };
    private static readonly FieldDefinition VarTableField = new() { Name = "Local Variables", GffPath = "VarTable", FieldType = SearchFieldType.Variable, Category = SearchFieldCategory.Variable, Description = "Local variable names and string values" };
    private static readonly FieldDefinition InventoryResField = new() { Name = "InventoryRes", GffPath = "InventoryRes", FieldType = SearchFieldType.ResRef, Category = SearchFieldCategory.Identity, Description = "Store inventory item ResRef", IsReplaceable = true };
    private static readonly FieldDefinition InventoryItemNameField = new() { Name = "InventoryItemName", GffPath = "InventoryRes", FieldType = SearchFieldType.Text, Category = SearchFieldCategory.Content, Description = "Resolved inventory item display name", IsReplaceable = false };

    private readonly Func<string, string?>? _itemNameResolver;

    /// <summary>
    /// Create a UtmSearchProvider with optional item name resolution.
    /// </summary>
    /// <param name="itemNameResolver">Optional callback that resolves an item ResRef to its display name.
    /// When provided, inventory items are searchable by their resolved name (e.g., "Club" instead of "nw_wblcl001").</param>
    public UtmSearchProvider(Func<string, string?>? itemNameResolver = null)
    {
        _itemNameResolver = itemNameResolver;
    }

    public ushort FileType => ResourceTypes.Utm;

    public IReadOnlyList<string> Extensions => new[] { ".utm" };

    public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria)
    {
        var bytes = GffWriter.Write(gffFile);
        var utm = UtmReader.Read(bytes);

        var regex = criteria.ToRegex();
        var matches = new List<SearchMatch>();

        if (criteria.MatchesField(NameField))
            matches.AddRange(SearchLocString(utm.LocName, NameField, regex, "LocName", criteria.EffectiveTlkResolver));
        if (criteria.MatchesField(TagField))
            matches.AddRange(SearchString(utm.Tag, TagField, regex, "Tag"));
        if (criteria.MatchesField(ResRefField))
            matches.AddRange(SearchString(utm.ResRef, ResRefField, regex, "ResRef"));
        if (criteria.MatchesField(CommentField))
            matches.AddRange(SearchString(utm.Comment, CommentField, regex, "Comment"));
        if (criteria.MatchesField(OnOpenStoreField))
            matches.AddRange(SearchString(utm.OnOpenStore, OnOpenStoreField, regex, "OnOpenStore"));
        if (criteria.MatchesField(OnStoreClosedField))
            matches.AddRange(SearchString(utm.OnStoreClosed, OnStoreClosedField, regex, "OnStoreClosed"));
        if (criteria.MatchesField(VarTableField))
            matches.AddRange(SearchVarTable(gffFile.RootStruct, VarTableField, regex, "VarTable"));

        // Search inventory item ResRefs and resolved names across all store panels
        var searchResRef = criteria.MatchesField(InventoryResField);
        var searchItemName = _itemNameResolver != null && criteria.MatchesField(InventoryItemNameField);

        if (searchResRef || searchItemName)
        {
            foreach (var panel in utm.StoreList)
            {
                var panelName = StorePanels.GetPanelName(panel.PanelId);
                for (int i = 0; i < panel.Items.Count; i++)
                {
                    var item = panel.Items[i];
                    var location = $"{panelName} > Item {i} > InventoryRes";

                    if (searchResRef)
                        matches.AddRange(SearchString(item.InventoryRes, InventoryResField, regex, location));

                    if (searchItemName)
                    {
                        var displayName = _itemNameResolver!(item.InventoryRes);
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            var nameLocation = $"{panelName} > Item {i} > {displayName}";
                            matches.AddRange(SearchString(displayName, InventoryItemNameField, regex, nameLocation));
                        }
                    }
                }
            }
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
            var gffPath = op.Match.Field.GffPath;
            ReplaceResult result;

            if (op.Match.Field == InventoryResField)
            {
                result = ReplaceInventoryResRef(gffFile.RootStruct, op);
            }
            else
            {
                result = op.Match.Field.FieldType switch
                {
                    SearchFieldType.LocString => ReplaceLocStringField(gffFile.RootStruct, gffPath, op),
                    SearchFieldType.Variable => ReplaceVarTableField(gffFile.RootStruct, op),
                    _ => ReplaceStringField(gffFile.RootStruct, gffPath, op)
                };
            }
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Replace an InventoryRes value in a nested StoreList/ItemList GFF structure.
    /// Navigates: RootStruct["StoreList"][panelIndex]["ItemList"][itemIndex]["InventoryRes"]
    /// </summary>
    private static ReplaceResult ReplaceInventoryResRef(GffStruct rootStruct, ReplaceOperation op)
    {
        // Parse location string to find panel and item indices
        // Format: "PanelName > Item N > InventoryRes"
        var location = op.Match.Location as string ?? string.Empty;
        if (!TryParseInventoryLocation(rootStruct, location, out var itemStruct))
        {
            return new ReplaceResult
            {
                Success = false, Field = op.Match.Field,
                OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                Skipped = true, SkipReason = $"Could not locate inventory item in GFF: {location}"
            };
        }

        var field = itemStruct.GetField("InventoryRes");
        if (field?.Value is not string currentValue)
        {
            return new ReplaceResult
            {
                Success = false, Field = op.Match.Field,
                OldValue = op.Match.FullFieldValue, NewValue = op.ReplacementText,
                Skipped = true, SkipReason = "InventoryRes field not found in GFF item struct"
            };
        }

        var (newValue, warning) = ReplaceResRef(currentValue, op);
        field.Value = newValue;

        return new ReplaceResult
        {
            Success = true, Field = op.Match.Field,
            OldValue = currentValue, NewValue = newValue,
            Warning = warning
        };
    }

    /// <summary>
    /// Parse a location string like "Weapons > Item 2 > InventoryRes" to find the
    /// corresponding GFF struct in StoreList/ItemList.
    /// </summary>
    private static bool TryParseInventoryLocation(GffStruct rootStruct, string location, out GffStruct itemStruct)
    {
        itemStruct = null!;

        // Parse "PanelName > Item N > InventoryRes"
        var parts = location.Split(" > ");
        if (parts.Length < 3) return false;

        var panelName = parts[0];
        if (!parts[1].StartsWith("Item ")) return false;
        if (!int.TryParse(parts[1].AsSpan(5), out var itemIndex)) return false;

        // Resolve panel name to panel ID
        var panelId = panelName switch
        {
            "Armor" => StorePanels.Armor,
            "Miscellaneous" => StorePanels.Miscellaneous,
            "Potions/Scrolls" => StorePanels.Potions,
            "Rings/Amulets" => StorePanels.RingsAmulets,
            "Weapons" => StorePanels.Weapons,
            _ => -1
        };
        if (panelId == -1) return false;

        // Navigate GFF: StoreList[panelIndex]["ItemList"][itemIndex]
        var storeListField = rootStruct.GetField("StoreList");
        if (storeListField?.Value is not GffList storeList) return false;

        // Find the panel struct by matching struct Type == panelId
        var panelStruct = storeList.Elements.FirstOrDefault(s => s.Type == (uint)panelId);
        if (panelStruct == null) return false;

        var itemListField = panelStruct.GetField("ItemList");
        if (itemListField?.Value is not GffList itemList) return false;

        if (itemIndex < 0 || itemIndex >= itemList.Elements.Count) return false;

        itemStruct = itemList.Elements[itemIndex];
        return true;
    }
}
