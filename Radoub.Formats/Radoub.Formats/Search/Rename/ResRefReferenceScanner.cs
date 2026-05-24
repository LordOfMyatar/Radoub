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

        if (resourceType == ResourceTypes.Dlg)
            ScanDlgNodes(gffFile, oldResRef, filePath, results);

        if (resourceType == ResourceTypes.Ifo)
            ScanIfoHakList(gffFile, oldResRef, filePath, results);

        return results;
    }

    private static readonly FieldDefinition ModHakField = new()
    {
        Name = "HAK",
        GffPath = "Mod_Hak",
        FieldType = SearchFieldType.ResRef,
        Category = SearchFieldCategory.Metadata,
        Description = "HAK file ResRef (per entry in Mod_HakList)",
        IsReplaceable = false
    };

    private void ScanIfoHakList(
        GffFile gff, string oldResRef, string filePath, List<ResRefReference> results)
    {
        var hakListField = gff.RootStruct.GetField("Mod_HakList");
        if (hakListField?.Value is not GffList hakList) return;

        for (int i = 0; i < hakList.Elements.Count; i++)
        {
            var f = hakList.Elements[i].GetField("Mod_Hak");
            if (f?.Value is string v
                && string.Equals(v, oldResRef, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(MakeRef(filePath, ResourceTypes.Ifo, ModHakField,
                    $"Mod_HakList > Item {i} > Mod_Hak", v));
            }
        }
    }

    private static readonly FieldDefinition SoundField = new()
    {
        Name = "Sound",
        GffPath = "Sound",
        FieldType = SearchFieldType.ResRef,
        Category = SearchFieldCategory.Metadata,
        Description = "Sound file reference",
        IsReplaceable = false
    };

    private void ScanDlgNodes(
        GffFile gff, string oldResRef, string filePath, List<ResRefReference> results)
    {
        ScanDlgNodeList(gff, "EntryList", "Entry", oldResRef, filePath, results);
        ScanDlgNodeList(gff, "ReplyList", "Reply", oldResRef, filePath, results);
    }

    private void ScanDlgNodeList(
        GffFile gff, string listFieldName, string nodeKind,
        string oldResRef, string filePath, List<ResRefReference> results)
    {
        var listField = gff.RootStruct.GetField(listFieldName);
        if (listField?.Value is not GffList nodes) return;

        for (int i = 0; i < nodes.Elements.Count; i++)
        {
            var node = nodes.Elements[i];

            // Sound field on the node itself
            var soundField = node.GetField("Sound");
            if (soundField?.Value is string v
                && string.Equals(v, oldResRef, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(MakeRef(filePath, ResourceTypes.Dlg, SoundField,
                    $"{nodeKind} {i} > Sound", v));
            }

            // ActionParams live directly on entry/reply nodes
            ScanDlgParams(node, "ActionParams", nodeKind, i, oldResRef, filePath, results);

            // ConditionParams live on link structs inside RepliesList (for entries)
            // or EntriesList (for replies). Walk those links per node.
            var linkListName = nodeKind == "Entry" ? "RepliesList" : "EntriesList";
            var linkListField = node.GetField(linkListName);
            if (linkListField?.Value is GffList linkList)
            {
                for (int linkIdx = 0; linkIdx < linkList.Elements.Count; linkIdx++)
                {
                    var link = linkList.Elements[linkIdx];
                    ScanDlgParams(
                        link, "ConditionParams",
                        $"{nodeKind} {i} > {linkListName}[{linkIdx}]", -1,
                        oldResRef, filePath, results);
                }
            }
        }
    }

    private void ScanDlgParams(
        GffStruct container, string paramFieldName, string nodeKind, int nodeIndex,
        string oldResRef, string filePath, List<ResRefReference> results)
    {
        var paramListField = container.GetField(paramFieldName);
        if (paramListField?.Value is not GffList paramList) return;

        for (int p = 0; p < paramList.Elements.Count; p++)
        {
            var key = paramList.Elements[p].GetField("Key")?.Value as string ?? string.Empty;
            var value = paramList.Elements[p].GetField("Value")?.Value as string ?? string.Empty;

            // Substring match in VALUE only (per spec Tier 2 — keys are parameter names)
            var idx = value.IndexOf(oldResRef, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            // Location format: when nodeIndex >= 0 (direct on node), use "{Kind} {N} > {field}..."
            // When nodeIndex < 0, the caller already provided the full prefix in nodeKind.
            var locationPrefix = nodeIndex >= 0
                ? $"{nodeKind} {nodeIndex} > {paramFieldName}[{p}] ({key})"
                : $"{nodeKind} > {paramFieldName}[{p}] ({key})";

            results.Add(new ResRefReference
            {
                FilePath = filePath,
                ResourceType = ResourceTypes.Dlg,
                Field = null,  // no registered FieldDefinition for params; orchestrator branches on ScopeTier
                Location = locationPrefix,
                OldValue = value,
                NewValue = string.Empty,
                ScopeTier = ResRefScopeTier.DlgScriptParam,
                MatchOffset = idx,
                MatchLength = oldResRef.Length
            });
        }
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
