using Radoub.Formats.Gff;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Utd;

/// <summary>
/// Writes UTD (Door) files to binary format.
/// UTD files are GFF-based with file type "UTD ".
/// Reference: BioWare Aurora Situated Object Format specification, neverwinter.nim
/// </summary>
public static class UtdWriter
{
    /// <summary>
    /// Write a UTD file to a file path.
    /// </summary>
    public static void Write(UtdFile utd, string filePath)
    {
        var buffer = Write(utd);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write a UTD file to a stream.
    /// </summary>
    public static void Write(UtdFile utd, Stream stream)
    {
        var buffer = Write(utd);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write a UTD file to a byte buffer.
    /// </summary>
    public static byte[] Write(UtdFile utd)
    {
        var gff = BuildGffFile(utd);
        return GffWriter.Write(gff);
    }

    private static GffFile BuildGffFile(UtdFile utd)
    {
        var gff = new GffFile
        {
            FileType = utd.FileType,
            FileVersion = utd.FileVersion,
            RootStruct = BuildRootStruct(utd)
        };

        return gff;
    }

    private static GffStruct BuildRootStruct(UtdFile utd)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Identity
        AddCResRefField(root, "TemplateResRef", utd.TemplateResRef);
        AddCExoStringField(root, "Tag", utd.Tag);
        AddLocStringField(root, "LocName", utd.LocName);
        AddLocStringField(root, "Description", utd.Description);

        // Appearance
        AddDwordField(root, "Appearance", utd.Appearance);
        AddByteField(root, "GenericType", utd.GenericType);
        AddByteField(root, "AnimationState", utd.AnimationState);
        AddWordField(root, "PortraitId", utd.PortraitId);

        // Combat / Physical
        AddShortField(root, "HP", utd.HP);
        AddShortField(root, "CurrentHP", utd.CurrentHP);
        AddByteField(root, "Hardness", utd.Hardness);
        AddByteField(root, "Fort", utd.Fort);
        AddByteField(root, "Ref", utd.Ref);
        AddByteField(root, "Will", utd.Will);
        AddByteField(root, "Plot", (byte)(utd.Plot ? 1 : 0));
        AddDwordField(root, "Faction", utd.Faction);
        AddByteField(root, "Interruptable", (byte)(utd.Interruptable ? 1 : 0));

        // Lock fields
        AddByteField(root, "Lockable", (byte)(utd.Lockable ? 1 : 0));
        AddByteField(root, "Locked", (byte)(utd.Locked ? 1 : 0));
        AddByteField(root, "OpenLockDC", utd.OpenLockDC);
        AddByteField(root, "CloseLockDC", utd.CloseLockDC);
        AddByteField(root, "AutoRemoveKey", (byte)(utd.AutoRemoveKey ? 1 : 0));
        if (!string.IsNullOrEmpty(utd.KeyName))
            AddCExoStringField(root, "KeyName", utd.KeyName);
        AddByteField(root, "KeyRequired", (byte)(utd.KeyRequired ? 1 : 0));

        // Trap fields
        AddByteField(root, "TrapFlag", (byte)(utd.TrapFlag ? 1 : 0));
        AddByteField(root, "TrapType", utd.TrapType);
        AddByteField(root, "TrapDetectable", (byte)(utd.TrapDetectable ? 1 : 0));
        AddByteField(root, "TrapDetectDC", utd.TrapDetectDC);
        AddByteField(root, "TrapDisarmable", (byte)(utd.TrapDisarmable ? 1 : 0));
        AddByteField(root, "DisarmDC", utd.DisarmDC);
        AddByteField(root, "TrapOneShot", (byte)(utd.TrapOneShot ? 1 : 0));

        // Door-specific
        if (!string.IsNullOrEmpty(utd.LinkedTo))
            AddCExoStringField(root, "LinkedTo", utd.LinkedTo);
        AddByteField(root, "LinkedToFlags", utd.LinkedToFlags);
        AddWordField(root, "LoadScreenID", utd.LoadScreenID);

        // Scripts
        if (!string.IsNullOrEmpty(utd.OnClick))
            AddCResRefField(root, "OnClick", utd.OnClick);
        if (!string.IsNullOrEmpty(utd.OnClosed))
            AddCResRefField(root, "OnClosed", utd.OnClosed);
        if (!string.IsNullOrEmpty(utd.OnDamaged))
            AddCResRefField(root, "OnDamaged", utd.OnDamaged);
        if (!string.IsNullOrEmpty(utd.OnDeath))
            AddCResRefField(root, "OnDeath", utd.OnDeath);
        if (!string.IsNullOrEmpty(utd.OnDisarm))
            AddCResRefField(root, "OnDisarm", utd.OnDisarm);
        if (!string.IsNullOrEmpty(utd.OnFailToOpen))
            AddCResRefField(root, "OnFailToOpen", utd.OnFailToOpen);
        if (!string.IsNullOrEmpty(utd.OnHeartbeat))
            AddCResRefField(root, "OnHeartbeat", utd.OnHeartbeat);
        if (!string.IsNullOrEmpty(utd.OnLock))
            AddCResRefField(root, "OnLock", utd.OnLock);
        if (!string.IsNullOrEmpty(utd.OnMeleeAttacked))
            AddCResRefField(root, "OnMeleeAttacked", utd.OnMeleeAttacked);
        if (!string.IsNullOrEmpty(utd.OnOpen))
            AddCResRefField(root, "OnOpen", utd.OnOpen);
        if (!string.IsNullOrEmpty(utd.OnSpellCastAt))
            AddCResRefField(root, "OnSpellCastAt", utd.OnSpellCastAt);
        if (!string.IsNullOrEmpty(utd.OnTrapTriggered))
            AddCResRefField(root, "OnTrapTriggered", utd.OnTrapTriggered);
        if (!string.IsNullOrEmpty(utd.OnUnlock))
            AddCResRefField(root, "OnUnlock", utd.OnUnlock);
        if (!string.IsNullOrEmpty(utd.OnUserDefined))
            AddCResRefField(root, "OnUserDefined", utd.OnUserDefined);

        // Metadata
        if (!string.IsNullOrEmpty(utd.Conversation))
            AddCResRefField(root, "Conversation", utd.Conversation);
        if (!string.IsNullOrEmpty(utd.Comment))
            AddCExoStringField(root, "Comment", utd.Comment);
        AddByteField(root, "PaletteID", utd.PaletteID);

        // Local variables
        VarTableHelper.WriteVarTable(root, utd.VarTable);

        return root;
    }
}
