using Radoub.Formats.Gff;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Ifo;

/// <summary>
/// Writes IFO (module.ifo) files to GFF format.
/// Reference: BioWare Aurora IFO Format specification
/// </summary>
public static class IfoWriter
{
    /// <summary>
    /// Write an IFO file to a file path.
    /// </summary>
    public static void Write(IfoFile ifo, string filePath)
    {
        var gff = ToGff(ifo);
        GffWriter.Write(gff, filePath);
    }

    /// <summary>
    /// Write an IFO file to a byte array.
    /// </summary>
    public static byte[] Write(IfoFile ifo)
    {
        var gff = ToGff(ifo);
        using var ms = new MemoryStream();
        GffWriter.Write(gff, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Write an IFO file to a stream.
    /// </summary>
    public static void Write(IfoFile ifo, Stream stream)
    {
        var gff = ToGff(ifo);
        GffWriter.Write(gff, stream);
    }

    /// <summary>
    /// Convert an IfoFile model to GFF format.
    /// </summary>
    public static GffFile ToGff(IfoFile ifo)
    {
        var gff = new GffFile
        {
            FileType = ifo.FileType,
            FileVersion = ifo.FileVersion,
            RootStruct = new GffStruct { Type = uint.MaxValue }
        };

        var root = gff.RootStruct;

        // Module Metadata
        AddLocStringField(root, "Mod_Name", ifo.ModuleName);
        AddLocStringField(root, "Mod_Description", ifo.ModuleDescription);
        AddCExoStringField(root, "Mod_Tag", ifo.Tag);
        if (!string.IsNullOrEmpty(ifo.ModuleId))
            AddCExoStringField(root, "Mod_ID", ifo.ModuleId);
        if (!string.IsNullOrEmpty(ifo.CustomTlk))
            AddCExoStringField(root, "Mod_CustomTlk", ifo.CustomTlk);

        // Version/Requirements
        AddCExoStringField(root, "Mod_MinGameVer", ifo.MinGameVersion);
        AddWordField(root, "Expansion_Pack", ifo.ExpansionPack);

        // HAK List
        WriteHakList(root, ifo.HakList);

        // Time Settings
        AddByteField(root, "Mod_DawnHour", ifo.DawnHour);
        AddByteField(root, "Mod_DuskHour", ifo.DuskHour);
        AddByteField(root, "Mod_MinPerHour", ifo.MinutesPerHour);
        AddDwordField(root, "Mod_StartYear", ifo.StartYear);
        AddByteField(root, "Mod_StartMonth", ifo.StartMonth);
        AddByteField(root, "Mod_StartDay", ifo.StartDay);
        AddByteField(root, "Mod_StartHour", ifo.StartHour);

        // Entry Point
        AddCResRefField(root, "Mod_Entry_Area", ifo.EntryArea);
        AddFloatField(root, "Mod_Entry_X", ifo.EntryX);
        AddFloatField(root, "Mod_Entry_Y", ifo.EntryY);
        AddFloatField(root, "Mod_Entry_Z", ifo.EntryZ);
        AddFloatField(root, "Mod_Entry_Dir_X", ifo.EntryDirX);
        AddFloatField(root, "Mod_Entry_Dir_Y", ifo.EntryDirY);

        // Module Scripts
        AddCResRefField(root, "Mod_OnModLoad", ifo.OnModuleLoad);
        AddCResRefField(root, "Mod_OnClientEntr", ifo.OnClientEnter);
        AddCResRefField(root, "Mod_OnClientLeav", ifo.OnClientLeave);
        AddCResRefField(root, "Mod_OnHeartbeat", ifo.OnHeartbeat);
        AddCResRefField(root, "Mod_OnAcquirItem", ifo.OnAcquireItem);
        AddCResRefField(root, "Mod_OnActvtItem", ifo.OnActivateItem);
        AddCResRefField(root, "Mod_OnUnAqreItem", ifo.OnUnacquireItem);
        AddCResRefField(root, "Mod_OnPlrDeath", ifo.OnPlayerDeath);
        AddCResRefField(root, "Mod_OnPlrDying", ifo.OnPlayerDying);
        AddCResRefField(root, "Mod_OnPlrRest", ifo.OnPlayerRest);
        AddCResRefField(root, "Mod_OnPlrEqItm", ifo.OnPlayerEquipItem);
        AddCResRefField(root, "Mod_OnPlrUnEqItm", ifo.OnPlayerUnequipItem);
        AddCResRefField(root, "Mod_OnPlrLvlUp", ifo.OnPlayerLevelUp);
        AddCResRefField(root, "Mod_OnUsrDefined", ifo.OnUserDefined);
        AddCResRefField(root, "Mod_OnSpawnBtnDn", ifo.OnSpawnButtonDown);
        AddCResRefField(root, "Mod_OnCutsnAbort", ifo.OnCutsceneAbort);

        // Other Settings
        AddByteField(root, "Mod_XPScale", ifo.XPScale);
        if (!string.IsNullOrEmpty(ifo.Creator))
            AddCExoStringField(root, "Mod_Creator_ID", ifo.Creator);
        AddDwordField(root, "Mod_Version", ifo.ModuleVersion);

        // Area List (preserve from original - don't modify)
        WriteAreaList(root, ifo.AreaList);

        // Local Variables
        VarTableHelper.WriteVarTable(root, ifo.VarTable);

        return gff;
    }

    /// <summary>
    /// Write the HAK list to a GFF struct.
    /// </summary>
    private static void WriteHakList(GffStruct root, List<string> hakList)
    {
        if (hakList.Count == 0)
            return;

        var list = new GffList();
        foreach (var hakName in hakList)
        {
            var hakStruct = new GffStruct { Type = 0 };
            AddCExoStringField(hakStruct, "Mod_Hak", hakName);
            list.Elements.Add(hakStruct);
        }

        AddListField(root, "Mod_HakList", list);
    }

    /// <summary>
    /// Write the area list to a GFF struct.
    /// </summary>
    private static void WriteAreaList(GffStruct root, List<string> areaList)
    {
        if (areaList.Count == 0)
            return;

        var list = new GffList();
        foreach (var areaResRef in areaList)
        {
            var areaStruct = new GffStruct { Type = 6 };
            AddCResRefField(areaStruct, "Area_Name", areaResRef);
            list.Elements.Add(areaStruct);
        }

        AddListField(root, "Mod_Area_List", list);
    }
}
