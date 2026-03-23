using Radoub.Formats.Gff;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Utp;

/// <summary>
/// Writes UTP (Placeable) files to binary format.
/// UTP files are GFF-based with file type "UTP ".
/// Reference: BioWare Aurora Situated Object Format specification, neverwinter.nim
/// </summary>
public static class UtpWriter
{
    /// <summary>
    /// Write a UTP file to a file path.
    /// </summary>
    public static void Write(UtpFile utp, string filePath)
    {
        var buffer = Write(utp);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write a UTP file to a stream.
    /// </summary>
    public static void Write(UtpFile utp, Stream stream)
    {
        var buffer = Write(utp);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write a UTP file to a byte buffer.
    /// </summary>
    public static byte[] Write(UtpFile utp)
    {
        var gff = BuildGffFile(utp);
        return GffWriter.Write(gff);
    }

    private static GffFile BuildGffFile(UtpFile utp)
    {
        var gff = new GffFile
        {
            FileType = utp.FileType,
            FileVersion = utp.FileVersion,
            RootStruct = BuildRootStruct(utp)
        };

        return gff;
    }

    private static GffStruct BuildRootStruct(UtpFile utp)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Identity
        AddCResRefField(root, "TemplateResRef", utp.TemplateResRef);
        AddCExoStringField(root, "Tag", utp.Tag);
        AddLocStringField(root, "LocName", utp.LocName);
        AddLocStringField(root, "Description", utp.Description);

        // Appearance
        AddDwordField(root, "Appearance", utp.Appearance);
        AddByteField(root, "AnimationState", utp.AnimationState);
        AddWordField(root, "PortraitId", utp.PortraitId);

        // Combat / Physical
        AddShortField(root, "HP", utp.HP);
        AddShortField(root, "CurrentHP", utp.CurrentHP);
        AddByteField(root, "Hardness", utp.Hardness);
        AddByteField(root, "Fort", utp.Fort);
        AddByteField(root, "Ref", utp.Ref);
        AddByteField(root, "Will", utp.Will);
        AddByteField(root, "Plot", (byte)(utp.Plot ? 1 : 0));
        AddDwordField(root, "Faction", utp.Faction);
        AddByteField(root, "Interruptable", (byte)(utp.Interruptable ? 1 : 0));

        // Lock fields
        AddByteField(root, "Lockable", (byte)(utp.Lockable ? 1 : 0));
        AddByteField(root, "Locked", (byte)(utp.Locked ? 1 : 0));
        AddByteField(root, "OpenLockDC", utp.OpenLockDC);
        AddByteField(root, "CloseLockDC", utp.CloseLockDC);
        AddByteField(root, "AutoRemoveKey", (byte)(utp.AutoRemoveKey ? 1 : 0));
        if (!string.IsNullOrEmpty(utp.KeyName))
            AddCExoStringField(root, "KeyName", utp.KeyName);
        AddByteField(root, "KeyRequired", (byte)(utp.KeyRequired ? 1 : 0));

        // Trap fields
        AddByteField(root, "TrapFlag", (byte)(utp.TrapFlag ? 1 : 0));
        AddByteField(root, "TrapType", utp.TrapType);
        AddByteField(root, "TrapDetectable", (byte)(utp.TrapDetectable ? 1 : 0));
        AddByteField(root, "TrapDetectDC", utp.TrapDetectDC);
        AddByteField(root, "TrapDisarmable", (byte)(utp.TrapDisarmable ? 1 : 0));
        AddByteField(root, "DisarmDC", utp.DisarmDC);
        AddByteField(root, "TrapOneShot", (byte)(utp.TrapOneShot ? 1 : 0));

        // Placeable-specific
        AddByteField(root, "HasInventory", (byte)(utp.HasInventory ? 1 : 0));
        AddByteField(root, "Useable", (byte)(utp.Useable ? 1 : 0));
        AddByteField(root, "Static", (byte)(utp.Static ? 1 : 0));
        AddByteField(root, "Type", utp.Type);
        AddByteField(root, "BodyBag", utp.BodyBag);

        // Scripts
        if (!string.IsNullOrEmpty(utp.OnClosed))
            AddCResRefField(root, "OnClosed", utp.OnClosed);
        if (!string.IsNullOrEmpty(utp.OnDamaged))
            AddCResRefField(root, "OnDamaged", utp.OnDamaged);
        if (!string.IsNullOrEmpty(utp.OnDeath))
            AddCResRefField(root, "OnDeath", utp.OnDeath);
        if (!string.IsNullOrEmpty(utp.OnDisarm))
            AddCResRefField(root, "OnDisarm", utp.OnDisarm);
        if (!string.IsNullOrEmpty(utp.OnHeartbeat))
            AddCResRefField(root, "OnHeartbeat", utp.OnHeartbeat);
        if (!string.IsNullOrEmpty(utp.OnInvDisturbed))
            AddCResRefField(root, "OnInvDisturbed", utp.OnInvDisturbed);
        if (!string.IsNullOrEmpty(utp.OnLock))
            AddCResRefField(root, "OnLock", utp.OnLock);
        if (!string.IsNullOrEmpty(utp.OnMeleeAttacked))
            AddCResRefField(root, "OnMeleeAttacked", utp.OnMeleeAttacked);
        if (!string.IsNullOrEmpty(utp.OnOpen))
            AddCResRefField(root, "OnOpen", utp.OnOpen);
        if (!string.IsNullOrEmpty(utp.OnSpellCastAt))
            AddCResRefField(root, "OnSpellCastAt", utp.OnSpellCastAt);
        if (!string.IsNullOrEmpty(utp.OnTrapTriggered))
            AddCResRefField(root, "OnTrapTriggered", utp.OnTrapTriggered);
        if (!string.IsNullOrEmpty(utp.OnUnlock))
            AddCResRefField(root, "OnUnlock", utp.OnUnlock);
        if (!string.IsNullOrEmpty(utp.OnUserDefined))
            AddCResRefField(root, "OnUserDefined", utp.OnUserDefined);
        if (!string.IsNullOrEmpty(utp.OnUsed))
            AddCResRefField(root, "OnUsed", utp.OnUsed);

        // Metadata
        if (!string.IsNullOrEmpty(utp.Conversation))
            AddCResRefField(root, "Conversation", utp.Conversation);
        if (!string.IsNullOrEmpty(utp.Comment))
            AddCExoStringField(root, "Comment", utp.Comment);
        AddByteField(root, "PaletteID", utp.PaletteID);

        // Item list
        if (utp.ItemList.Count > 0)
            AddItemList(root, utp.ItemList);

        // Local variables
        VarTableHelper.WriteVarTable(root, utp.VarTable);

        return root;
    }

    private static void AddItemList(GffStruct parent, List<PlaceableItem> items)
    {
        var list = new GffList();
        foreach (var item in items)
        {
            var itemStruct = new GffStruct { Type = 0 };
            AddCResRefField(itemStruct, "InventoryRes", item.InventoryRes);
            AddWordField(itemStruct, "Repos_PosX", item.Repos_PosX);
            AddWordField(itemStruct, "Repos_PosY", item.Repos_PosY);
            list.Elements.Add(itemStruct);
        }
        AddListField(parent, "ItemList", list);
    }
}
