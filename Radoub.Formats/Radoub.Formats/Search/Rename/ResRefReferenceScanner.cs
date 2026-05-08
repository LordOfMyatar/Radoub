using Radoub.Formats.Common;
using Radoub.Formats.Gff;

namespace Radoub.Formats.Search.Rename;

/// <summary>
/// Pure logic: given a parsed GFF and an old ResRef, returns every
/// reference site to that ResRef inside the file. No I/O.
/// See spec Section 4 (NonPublic/Trebuchet/2026-05-03-resref-rename-design.md).
///
/// Top-level scalar ResRef fields are discovered via SearchFieldRegistry.
/// Nested structures (GIT instance lists, UTC equipment/inventory, UTM store
/// panels, IFO HAK list, DLG node walks) are handled by per-resource-type
/// methods because they require list traversal that the registry doesn't model.
/// </summary>
public class ResRefReferenceScanner
{
    private readonly SearchFieldRegistry _registry;

    public ResRefReferenceScanner(SearchFieldRegistry? registry = null)
    {
        _registry = registry ?? CreateDefault();
    }

    private static SearchFieldRegistry CreateDefault()
    {
        var registry = new SearchFieldRegistry();
        FieldRegistrations.RegisterAll(registry);
        return registry;
    }

    public IReadOnlyList<ResRefReference> Scan(
        GffFile gffFile,
        ushort resourceType,
        string oldResRef,
        string filePath)
    {
        var results = new List<ResRefReference>();

        ScanTopLevelScalarFields(gffFile, resourceType, oldResRef, filePath, results);

        if (resourceType == ResourceTypes.Git)
            ScanGitInstanceLists(gffFile, oldResRef, filePath, results);

        if (resourceType == ResourceTypes.Utc || resourceType == ResourceTypes.Bic)
            ScanUtcEquipAndInventory(gffFile, resourceType, oldResRef, filePath, results);

        if (resourceType == ResourceTypes.Utp)
            ScanUtpInventory(gffFile, oldResRef, filePath, results);

        if (resourceType == ResourceTypes.Utm)
            ScanUtmStorePanels(gffFile, oldResRef, filePath, results);

        return results;
    }

    private void ScanUtmStorePanels(
        GffFile gff, string oldResRef, string filePath, List<ResRefReference> results)
    {
        var storeListField = gff.RootStruct.GetField("StoreList");
        if (storeListField?.Value is not GffList storeList) return;

        foreach (var panel in storeList.Elements)
        {
            // UTM panel name comes from the struct Type discriminator (StorePanels constants).
            var panelName = Utm.StorePanels.GetPanelName((int)panel.Type);

            var itemListField = panel.GetField("ItemList");
            if (itemListField?.Value is not GffList itemList) continue;

            for (int i = 0; i < itemList.Elements.Count; i++)
            {
                var f = itemList.Elements[i].GetField("InventoryRes");
                if (f?.Value is string v
                    && string.Equals(v, oldResRef, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(MakeRef(filePath, ResourceTypes.Utm, InventoryResField,
                        $"{panelName} > Item {i} > InventoryRes", v));
                }
            }
        }
    }

    private void ScanUtpInventory(
        GffFile gff, string oldResRef, string filePath, List<ResRefReference> results)
    {
        var itemListField = gff.RootStruct.GetField("ItemList");
        if (itemListField?.Value is not GffList itemList) return;

        for (int i = 0; i < itemList.Elements.Count; i++)
        {
            var f = itemList.Elements[i].GetField("InventoryRes");
            if (f?.Value is string v
                && string.Equals(v, oldResRef, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(MakeRef(filePath, ResourceTypes.Utp, InventoryResField,
                    $"ItemList > Item {i} > InventoryRes", v));
            }
        }
    }

    private static readonly FieldDefinition EquipResField = new()
    {
        Name = "EquipRes",
        GffPath = "EquipRes",
        FieldType = SearchFieldType.ResRef,
        Category = SearchFieldCategory.Identity,
        Description = "Equipped item ResRef",
        IsReplaceable = false
    };

    private static readonly FieldDefinition InventoryResField = new()
    {
        Name = "InventoryRes",
        GffPath = "InventoryRes",
        FieldType = SearchFieldType.ResRef,
        Category = SearchFieldCategory.Identity,
        Description = "Inventory item ResRef",
        IsReplaceable = false
    };

    private static ResRefReference MakeRef(
        string filePath, ushort resourceType, FieldDefinition field,
        string location, string oldValue) => new()
    {
        FilePath = filePath,
        ResourceType = resourceType,
        Field = field,
        Location = location,
        OldValue = oldValue,
        NewValue = string.Empty,
        ScopeTier = ResRefScopeTier.TypedGffField
    };

    private void ScanUtcEquipAndInventory(
        GffFile gff, ushort resourceType, string oldResRef, string filePath, List<ResRefReference> results)
    {
        var equipField = gff.RootStruct.GetField("Equip_ItemList");
        if (equipField?.Value is GffList equipList)
        {
            for (int i = 0; i < equipList.Elements.Count; i++)
            {
                var f = equipList.Elements[i].GetField("EquipRes");
                if (f?.Value is string v
                    && string.Equals(v, oldResRef, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(MakeRef(filePath, resourceType, EquipResField,
                        $"Equip_ItemList > Slot {i} > EquipRes", v));
                }
            }
        }

        var itemListField = gff.RootStruct.GetField("ItemList");
        if (itemListField?.Value is GffList itemList)
        {
            for (int i = 0; i < itemList.Elements.Count; i++)
            {
                var f = itemList.Elements[i].GetField("InventoryRes");
                if (f?.Value is string v
                    && string.Equals(v, oldResRef, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(MakeRef(filePath, resourceType, InventoryResField,
                        $"ItemList > Item {i} > InventoryRes", v));
                }
            }
        }
    }

    private static readonly (string ListName, string ResRefField)[] GitInstanceLists =
    {
        ("Creature List",  "TemplateResRef"),
        ("Door List",      "TemplateResRef"),
        ("Placeable List", "TemplateResRef"),
        ("StoreList",      "ResRef"),
        ("WaypointList",   "TemplateResRef"),
        ("Encounter List", "TemplateResRef"),
        ("TriggerList",    "TemplateResRef"),
        ("SoundList",      "TemplateResRef")
    };

    private static readonly FieldDefinition GitTemplateResRefField = new()
    {
        Name = "Template ResRef",
        GffPath = "TemplateResRef",
        FieldType = SearchFieldType.ResRef,
        Category = SearchFieldCategory.Identity,
        Description = "Blueprint resource reference",
        IsReplaceable = false
    };

    private static readonly FieldDefinition GitStoreResRefField = new()
    {
        Name = "ResRef",
        GffPath = "ResRef",
        FieldType = SearchFieldType.ResRef,
        Category = SearchFieldCategory.Identity,
        Description = "Store resource reference",
        IsReplaceable = false
    };

    private void ScanGitInstanceLists(
        GffFile gff, string oldResRef, string filePath, List<ResRefReference> results)
    {
        foreach (var (listName, resRefField) in GitInstanceLists)
        {
            var listField = gff.RootStruct.GetField(listName);
            if (listField?.Value is not GffList list) continue;

            for (int i = 0; i < list.Elements.Count; i++)
            {
                var rrField = list.Elements[i].GetField(resRefField);
                if (rrField?.Value is string value
                    && string.Equals(value, oldResRef, StringComparison.OrdinalIgnoreCase))
                {
                    var fieldDef = resRefField == "ResRef" ? GitStoreResRefField : GitTemplateResRefField;
                    results.Add(new ResRefReference
                    {
                        FilePath = filePath,
                        ResourceType = ResourceTypes.Git,
                        Field = fieldDef,
                        Location = $"{listName} > Item {i} > {resRefField}",
                        OldValue = value,
                        NewValue = string.Empty,
                        ScopeTier = ResRefScopeTier.TypedGffField
                    });
                }
            }
        }
    }

    private void ScanTopLevelScalarFields(
        GffFile gff, ushort resourceType, string oldResRef, string filePath,
        List<ResRefReference> results)
    {
        var fields = _registry.GetSearchableFields(resourceType);

        foreach (var field in fields)
        {
            // ResRef-carrier types: ResRef and Script (scripts are .nss/.ncs ResRefs)
            if (field.FieldType != SearchFieldType.ResRef && field.FieldType != SearchFieldType.Script)
                continue;

            // Top-level lookup only. Nested traversal is per-type below.
            var gffField = gff.RootStruct.GetField(field.GffPath);
            if (gffField?.Value is string value
                && string.Equals(value, oldResRef, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new ResRefReference
                {
                    FilePath = filePath,
                    ResourceType = resourceType,
                    Field = field,
                    Location = field.Name,
                    OldValue = value,
                    NewValue = string.Empty,  // orchestrator fills NewValue at plan-build time
                    ScopeTier = ResRefScopeTier.TypedGffField
                });
            }
        }
    }
}
