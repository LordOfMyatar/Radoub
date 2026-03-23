using Radoub.Formats.Gff;

namespace Radoub.Formats.Utd;

/// <summary>
/// Reads UTD (Door) files from binary format.
/// UTD files are GFF-based with file type "UTD ".
/// Reference: BioWare Aurora Situated Object Format specification, neverwinter.nim
/// </summary>
public static class UtdReader
{
    /// <summary>
    /// Read a UTD file from a file path.
    /// </summary>
    public static UtdFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a UTD file from a stream.
    /// </summary>
    public static UtdFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a UTD file from a byte buffer.
    /// </summary>
    public static UtdFile Read(byte[] buffer)
    {
        var gff = GffReader.Read(buffer);

        if (gff.FileType.TrimEnd() != "UTD")
        {
            throw new InvalidDataException(
                $"Invalid UTD file type: '{gff.FileType}' (expected 'UTD ')");
        }

        return ParseUtdFile(gff);
    }

    private static UtdFile ParseUtdFile(GffFile gff)
    {
        var root = gff.RootStruct;

        var utd = new UtdFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion,

            // Identity
            TemplateResRef = root.GetFieldValue<string>("TemplateResRef", string.Empty),
            Tag = root.GetFieldValue<string>("Tag", string.Empty),

            // Appearance
            Appearance = root.GetFieldValue<uint>("Appearance", 0),
            GenericType = root.GetFieldValue<byte>("GenericType", 0),
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

            // Door-specific
            LinkedTo = root.GetFieldValue<string>("LinkedTo", string.Empty),
            LinkedToFlags = root.GetFieldValue<byte>("LinkedToFlags", 0),
            LoadScreenID = root.GetFieldValue<ushort>("LoadScreenID", 0),

            // Scripts
            OnClick = root.GetFieldValue<string>("OnClick", string.Empty),
            OnClosed = root.GetFieldValue<string>("OnClosed", string.Empty),
            OnDamaged = root.GetFieldValue<string>("OnDamaged", string.Empty),
            OnDeath = root.GetFieldValue<string>("OnDeath", string.Empty),
            OnDisarm = root.GetFieldValue<string>("OnDisarm", string.Empty),
            OnFailToOpen = root.GetFieldValue<string>("OnFailToOpen", string.Empty),
            OnHeartbeat = root.GetFieldValue<string>("OnHeartbeat", string.Empty),
            OnLock = root.GetFieldValue<string>("OnLock", string.Empty),
            OnMeleeAttacked = root.GetFieldValue<string>("OnMeleeAttacked", string.Empty),
            OnOpen = root.GetFieldValue<string>("OnOpen", string.Empty),
            OnSpellCastAt = root.GetFieldValue<string>("OnSpellCastAt", string.Empty),
            OnTrapTriggered = root.GetFieldValue<string>("OnTrapTriggered", string.Empty),
            OnUnlock = root.GetFieldValue<string>("OnUnlock", string.Empty),
            OnUserDefined = root.GetFieldValue<string>("OnUserDefined", string.Empty),

            // Metadata
            Conversation = root.GetFieldValue<string>("Conversation", string.Empty),
            Comment = root.GetFieldValue<string>("Comment", string.Empty),
            PaletteID = root.GetFieldValue<byte>("PaletteID", 0)
        };

        // Localized strings
        utd.LocName = ParseLocString(root, "LocName") ?? new CExoLocString();
        utd.Description = ParseLocString(root, "Description") ?? new CExoLocString();

        // Local variables
        utd.VarTable = VarTableHelper.ReadVarTable(root);

        return utd;
    }

    private static CExoLocString? ParseLocString(GffStruct root, string fieldName)
    {
        var field = root.GetField(fieldName);
        if (field == null || !field.IsCExoLocString || field.Value is not CExoLocString locString)
            return null;

        return locString;
    }
}
