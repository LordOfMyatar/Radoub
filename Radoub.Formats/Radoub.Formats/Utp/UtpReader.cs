using Radoub.Formats.Gff;

namespace Radoub.Formats.Utp;

/// <summary>
/// Reads UTP (Placeable) files from binary format.
/// UTP files are GFF-based with file type "UTP ".
/// Reference: BioWare Aurora Situated Object Format specification, neverwinter.nim
/// </summary>
public static class UtpReader
{
    /// <summary>
    /// Read a UTP file from a file path.
    /// </summary>
    public static UtpFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a UTP file from a stream.
    /// </summary>
    public static UtpFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a UTP file from a byte buffer.
    /// </summary>
    public static UtpFile Read(byte[] buffer)
    {
        var gff = GffReader.Read(buffer);

        if (gff.FileType.TrimEnd() != "UTP")
        {
            throw new InvalidDataException(
                $"Invalid UTP file type: '{gff.FileType}' (expected 'UTP ')");
        }

        return ParseUtpFile(gff);
    }

    private static UtpFile ParseUtpFile(GffFile gff)
    {
        var root = gff.RootStruct;

        var utp = new UtpFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion,

            // Identity
            TemplateResRef = root.GetFieldValue<string>("TemplateResRef", string.Empty),
            Tag = root.GetFieldValue<string>("Tag", string.Empty),

            // Appearance
            Appearance = root.GetFieldValue<uint>("Appearance", 0),
            AnimationState = root.GetFieldValue<byte>("AnimationState", 0),
            PortraitId = root.GetFieldValue<ushort>("PortraitId", 0),

            // Combat / Physical
            HP = root.GetFieldValue<short>("HP", 0),
            CurrentHP = root.GetFieldValue<short>("CurrentHP", 0),
            Hardness = root.GetFieldValue<byte>("Hardness", 0),
            Fort = root.GetFieldValue<byte>("Fort", 0),
            Ref = root.GetFieldValue<byte>("Ref", 0),
            Will = root.GetFieldValue<byte>("Will", 0),
            Plot = root.GetFieldValue<byte>("Plot", 0) != 0,
            Faction = root.GetFieldValue<uint>("Faction", 0),
            Interruptable = root.GetFieldValue<byte>("Interruptable", 1) != 0,

            // Lock fields
            Lockable = root.GetFieldValue<byte>("Lockable", 0) != 0,
            Locked = root.GetFieldValue<byte>("Locked", 0) != 0,
            OpenLockDC = root.GetFieldValue<byte>("OpenLockDC", 0),
            CloseLockDC = root.GetFieldValue<byte>("CloseLockDC", 0),
            AutoRemoveKey = root.GetFieldValue<byte>("AutoRemoveKey", 0) != 0,
            KeyName = root.GetFieldValue<string>("KeyName", string.Empty),
            KeyRequired = root.GetFieldValue<byte>("KeyRequired", 0) != 0,

            // Trap fields
            TrapFlag = root.GetFieldValue<byte>("TrapFlag", 0) != 0,
            TrapType = root.GetFieldValue<byte>("TrapType", 0),
            TrapDetectable = root.GetFieldValue<byte>("TrapDetectable", 1) != 0,
            TrapDetectDC = root.GetFieldValue<byte>("TrapDetectDC", 0),
            TrapDisarmable = root.GetFieldValue<byte>("TrapDisarmable", 1) != 0,
            DisarmDC = root.GetFieldValue<byte>("DisarmDC", 0),
            TrapOneShot = root.GetFieldValue<byte>("TrapOneShot", 1) != 0,

            // Placeable-specific
            HasInventory = root.GetFieldValue<byte>("HasInventory", 0) != 0,
            Useable = root.GetFieldValue<byte>("Useable", 1) != 0,
            Static = root.GetFieldValue<byte>("Static", 0) != 0,
            Type = root.GetFieldValue<byte>("Type", 0),
            BodyBag = root.GetFieldValue<byte>("BodyBag", 0),

            // Scripts
            OnClosed = root.GetFieldValue<string>("OnClosed", string.Empty),
            OnDamaged = root.GetFieldValue<string>("OnDamaged", string.Empty),
            OnDeath = root.GetFieldValue<string>("OnDeath", string.Empty),
            OnDisarm = root.GetFieldValue<string>("OnDisarm", string.Empty),
            OnHeartbeat = root.GetFieldValue<string>("OnHeartbeat", string.Empty),
            OnInvDisturbed = root.GetFieldValue<string>("OnInvDisturbed", string.Empty),
            OnLock = root.GetFieldValue<string>("OnLock", string.Empty),
            OnMeleeAttacked = root.GetFieldValue<string>("OnMeleeAttacked", string.Empty),
            OnOpen = root.GetFieldValue<string>("OnOpen", string.Empty),
            OnSpellCastAt = root.GetFieldValue<string>("OnSpellCastAt", string.Empty),
            OnTrapTriggered = root.GetFieldValue<string>("OnTrapTriggered", string.Empty),
            OnUnlock = root.GetFieldValue<string>("OnUnlock", string.Empty),
            OnUserDefined = root.GetFieldValue<string>("OnUserDefined", string.Empty),
            OnUsed = root.GetFieldValue<string>("OnUsed", string.Empty),

            // Metadata
            Conversation = root.GetFieldValue<string>("Conversation", string.Empty),
            Comment = root.GetFieldValue<string>("Comment", string.Empty),
            PaletteID = root.GetFieldValue<byte>("PaletteID", 0)
        };

        // Localized strings
        utp.LocName = ParseLocString(root, "LocName") ?? new CExoLocString();
        utp.Description = ParseLocString(root, "Description") ?? new CExoLocString();

        // Item list
        ParseItemList(root, utp);

        // Local variables
        utp.VarTable = VarTableHelper.ReadVarTable(root);

        return utp;
    }

    private static CExoLocString? ParseLocString(GffStruct root, string fieldName)
    {
        var field = root.GetField(fieldName);
        if (field == null || !field.IsCExoLocString || field.Value is not CExoLocString locString)
            return null;

        return locString;
    }

    private static void ParseItemList(GffStruct root, UtpFile utp)
    {
        var field = root.GetField("ItemList");
        if (field == null || !field.IsList || field.Value is not GffList itemList)
            return;

        foreach (var itemStruct in itemList.Elements)
        {
            var item = new PlaceableItem
            {
                InventoryRes = itemStruct.GetFieldValue<string>("InventoryRes", string.Empty),
                Repos_PosX = itemStruct.GetFieldValue<ushort>("Repos_PosX", 0),
                Repos_PosY = itemStruct.GetFieldValue<ushort>("Repos_PosY", 0)
            };

            utp.ItemList.Add(item);
        }
    }
}
