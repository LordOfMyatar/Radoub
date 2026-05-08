using Radoub.Formats.Gff;

namespace Radoub.Formats.Tests.Search.Rename;

/// <summary>
/// Builds in-memory GFF structures for ResRefReferenceScanner tests.
/// Each builder populates only the fields a specific test cares about;
/// other fields are left absent.
/// All ResRef-carrier fields use lowercase values per Aurora convention.
/// </summary>
internal static class TestGffBuilder
{
    // --- UTC ---
    public static GffFile MakeUtc(
        string? conversation = null,
        IReadOnlyList<string>? equipResRefs = null,
        IReadOnlyList<string>? inventoryResRefs = null)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        if (conversation != null)
            GffFieldBuilder.AddCResRefField(root, "Conversation", conversation);

        if (equipResRefs != null)
        {
            var slots = new List<GffStruct>();
            foreach (var rr in equipResRefs)
            {
                var slot = new GffStruct { Type = 0 };
                GffFieldBuilder.AddCResRefField(slot, "EquipRes", rr);
                slots.Add(slot);
            }
            GffFieldBuilder.AddListField(root, "Equip_ItemList", slots);
        }

        if (inventoryResRefs != null)
        {
            var items = new List<GffStruct>();
            foreach (var rr in inventoryResRefs)
            {
                var item = new GffStruct { Type = 0 };
                GffFieldBuilder.AddCResRefField(item, "InventoryRes", rr);
                items.Add(item);
            }
            GffFieldBuilder.AddListField(root, "ItemList", items);
        }

        return new GffFile { FileType = "UTC ", FileVersion = "V3.2", RootStruct = root };
    }

    // --- UTI ---
    public static GffFile MakeUti(string? onAcquireScript = null)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        if (onAcquireScript != null)
            GffFieldBuilder.AddCResRefField(root, "OnAcquireItem", onAcquireScript);
        return new GffFile { FileType = "UTI ", FileVersion = "V3.2", RootStruct = root };
    }

    // --- UTM ---
    public static GffFile MakeUtmWithItems(
        string panelName,
        params string[] itemResRefs)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var storeList = new List<GffStruct>();

        // Map panel name to canonical store panel ID (used as struct.Type per UtmReader/Writer)
        var panelId = panelName switch
        {
            "Armor" => Radoub.Formats.Utm.StorePanels.Armor,
            "Miscellaneous" => Radoub.Formats.Utm.StorePanels.Miscellaneous,
            "Potions/Scrolls" => Radoub.Formats.Utm.StorePanels.Potions,
            "Rings/Amulets" => Radoub.Formats.Utm.StorePanels.RingsAmulets,
            "Weapons" => Radoub.Formats.Utm.StorePanels.Weapons,
            _ => 0
        };
        var panel = new GffStruct { Type = (uint)panelId };

        var items = new List<GffStruct>();
        foreach (var rr in itemResRefs)
        {
            var item = new GffStruct { Type = 0 };
            GffFieldBuilder.AddCResRefField(item, "InventoryRes", rr);
            items.Add(item);
        }
        GffFieldBuilder.AddListField(panel, "ItemList", items);
        storeList.Add(panel);
        GffFieldBuilder.AddListField(root, "StoreList", storeList);

        return new GffFile { FileType = "UTM ", FileVersion = "V3.2", RootStruct = root };
    }

    // --- UTP ---
    public static GffFile MakeUtpWithInventory(params string[] inventoryResRefs)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var items = new List<GffStruct>();
        foreach (var rr in inventoryResRefs)
        {
            var item = new GffStruct { Type = 0 };
            GffFieldBuilder.AddCResRefField(item, "InventoryRes", rr);
            items.Add(item);
        }
        GffFieldBuilder.AddListField(root, "ItemList", items);
        return new GffFile { FileType = "UTP ", FileVersion = "V3.2", RootStruct = root };
    }

    // --- UTD ---
    public static GffFile MakeUtd(string? conversation = null)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        if (conversation != null)
            GffFieldBuilder.AddCResRefField(root, "Conversation", conversation);
        return new GffFile { FileType = "UTD ", FileVersion = "V3.2", RootStruct = root };
    }

    public static GffFile MakeUtdWithScriptField(string fieldName, string scriptResRef)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddCResRefField(root, fieldName, scriptResRef);
        return new GffFile { FileType = "UTD ", FileVersion = "V3.2", RootStruct = root };
    }

    // --- DLG ---
    public static GffFile MakeDlgWithSound(int entryIndex, string sound)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var entries = new List<GffStruct>();
        for (int i = 0; i <= entryIndex; i++)
        {
            var entry = new GffStruct { Type = 0 };
            if (i == entryIndex)
                GffFieldBuilder.AddCResRefField(entry, "Sound", sound);
            entries.Add(entry);
        }
        GffFieldBuilder.AddListField(root, "EntryList", entries);
        return new GffFile { FileType = "DLG ", FileVersion = "V3.2", RootStruct = root };
    }

    public static GffFile MakeDlgWithActionParam(int entryIndex, string key, string value)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var entries = new List<GffStruct>();
        for (int i = 0; i <= entryIndex; i++)
        {
            var entry = new GffStruct { Type = 0 };
            var actionParams = new List<GffStruct>();
            if (i == entryIndex)
            {
                var param = new GffStruct { Type = 0 };
                GffFieldBuilder.AddCExoStringField(param, "Key", key);
                GffFieldBuilder.AddCExoStringField(param, "Value", value);
                actionParams.Add(param);
            }
            GffFieldBuilder.AddListField(entry, "ActionParams", actionParams);
            entries.Add(entry);
        }
        GffFieldBuilder.AddListField(root, "EntryList", entries);
        return new GffFile { FileType = "DLG ", FileVersion = "V3.2", RootStruct = root };
    }

    public static GffFile MakeDlgWithConditionParam(int entryIndex, string key, string value)
    {
        // ConditionParams live on link structs inside RepliesList (per DLG spec).
        // Build an entry with a single reply link that carries the condition param.
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var entries = new List<GffStruct>();
        for (int i = 0; i <= entryIndex; i++)
        {
            var entry = new GffStruct { Type = 0 };
            var repliesList = new List<GffStruct>();
            if (i == entryIndex)
            {
                var link = new GffStruct { Type = 0 };
                var condParams = new List<GffStruct>();
                var param = new GffStruct { Type = 0 };
                GffFieldBuilder.AddCExoStringField(param, "Key", key);
                GffFieldBuilder.AddCExoStringField(param, "Value", value);
                condParams.Add(param);
                GffFieldBuilder.AddListField(link, "ConditionParams", condParams);
                repliesList.Add(link);
            }
            GffFieldBuilder.AddListField(entry, "RepliesList", repliesList);
            entries.Add(entry);
        }
        GffFieldBuilder.AddListField(root, "EntryList", entries);
        return new GffFile { FileType = "DLG ", FileVersion = "V3.2", RootStruct = root };
    }

    // --- GIT ---
    public static GffFile MakeGitWithList(string listName, string resRefField, params string[] resRefs)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var instances = new List<GffStruct>();
        foreach (var rr in resRefs)
        {
            var instance = new GffStruct { Type = 0 };
            GffFieldBuilder.AddCResRefField(instance, resRefField, rr);
            instances.Add(instance);
        }
        GffFieldBuilder.AddListField(root, listName, instances);
        return new GffFile { FileType = "GIT ", FileVersion = "V3.2", RootStruct = root };
    }

    // --- ARE ---
    public static GffFile MakeAre(string? onEnterScript = null)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        if (onEnterScript != null)
            GffFieldBuilder.AddCResRefField(root, "OnEnter", onEnterScript);
        return new GffFile { FileType = "ARE ", FileVersion = "V3.2", RootStruct = root };
    }

    public static GffFile MakeAreWithScriptField(string fieldName, string scriptResRef)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddCResRefField(root, fieldName, scriptResRef);
        return new GffFile { FileType = "ARE ", FileVersion = "V3.2", RootStruct = root };
    }

    // --- IFO ---
    public static GffFile MakeIfo(
        string? entryArea = null,
        string? defaultBic = null,
        string? startMovie = null,
        string? customTlk = null,
        string? onHeartbeat = null)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        if (entryArea != null) GffFieldBuilder.AddCResRefField(root, "Mod_Entry_Area", entryArea);
        if (defaultBic != null) GffFieldBuilder.AddCResRefField(root, "Mod_DefaultBic", defaultBic);
        if (startMovie != null) GffFieldBuilder.AddCResRefField(root, "Mod_StartMovie", startMovie);
        if (customTlk != null) GffFieldBuilder.AddCResRefField(root, "Mod_CustomTlk", customTlk);
        if (onHeartbeat != null) GffFieldBuilder.AddCResRefField(root, "Mod_OnHeartbeat", onHeartbeat);
        return new GffFile { FileType = "IFO ", FileVersion = "V3.2", RootStruct = root };
    }

    public static GffFile MakeIfoWithHakList(params string[] hakResRefs)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var haks = new List<GffStruct>();
        foreach (var rr in hakResRefs)
        {
            var hakStruct = new GffStruct { Type = 0 };
            GffFieldBuilder.AddCResRefField(hakStruct, "Mod_Hak", rr);
            haks.Add(hakStruct);
        }
        GffFieldBuilder.AddListField(root, "Mod_HakList", haks);
        return new GffFile { FileType = "IFO ", FileVersion = "V3.2", RootStruct = root };
    }
}
