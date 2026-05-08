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
        params string[] itemResRefs) => throw new NotImplementedException();

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
    public static GffFile MakeDlgWithSound(int entryIndex, string sound) => throw new NotImplementedException();
    public static GffFile MakeDlgWithActionParam(int entryIndex, string key, string value) => throw new NotImplementedException();
    public static GffFile MakeDlgWithConditionParam(int entryIndex, string key, string value) => throw new NotImplementedException();

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
        string? onPlayerHeartbeat = null) => throw new NotImplementedException();

    public static GffFile MakeIfoWithHakList(params string[] hakResRefs) => throw new NotImplementedException();
}
