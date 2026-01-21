using Radoub.Formats.Gff;

namespace Radoub.Formats.Ifo;

/// <summary>
/// Reads IFO (module.ifo) files from GFF format.
/// Reference: BioWare Aurora IFO Format specification
/// </summary>
public static class IfoReader
{
    /// <summary>
    /// Read an IFO file from a file path.
    /// </summary>
    public static IfoFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read an IFO file from a byte buffer.
    /// </summary>
    public static IfoFile Read(byte[] buffer)
    {
        var gff = GffReader.Read(buffer);
        return FromGff(gff);
    }

    /// <summary>
    /// Read an IFO file from a stream.
    /// </summary>
    public static IfoFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Convert a GFF file to an IfoFile model.
    /// </summary>
    public static IfoFile FromGff(GffFile gff)
    {
        var ifo = new IfoFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion
        };

        var root = gff.RootStruct;
        if (root == null)
            return ifo;

        // Module Metadata
        ifo.ModuleName = ReadLocString(root, "Mod_Name");
        ifo.ModuleDescription = ReadLocString(root, "Mod_Description");
        ifo.Tag = root.GetFieldValue<string>("Mod_Tag", string.Empty);
        ifo.ModuleId = root.GetFieldValue<string>("Mod_ID", string.Empty);
        ifo.CustomTlk = root.GetFieldValue<string>("Mod_CustomTlk", string.Empty);

        // Version/Requirements
        ifo.MinGameVersion = root.GetFieldValue<string>("Mod_MinGameVer", "1.69");
        ifo.ExpansionPack = root.GetFieldValue<ushort>("Expansion_Pack", 0);

        // HAK List
        ifo.HakList = ReadHakList(root);

        // Time Settings
        ifo.DawnHour = root.GetFieldValue<byte>("Mod_DawnHour", 6);
        ifo.DuskHour = root.GetFieldValue<byte>("Mod_DuskHour", 18);
        ifo.MinutesPerHour = root.GetFieldValue<byte>("Mod_MinPerHour", 2);
        ifo.StartYear = root.GetFieldValue<uint>("Mod_StartYear", 1372);
        ifo.StartMonth = root.GetFieldValue<byte>("Mod_StartMonth", 1);
        ifo.StartDay = root.GetFieldValue<byte>("Mod_StartDay", 1);
        ifo.StartHour = root.GetFieldValue<byte>("Mod_StartHour", 13);

        // Entry Point
        ifo.EntryArea = root.GetFieldValue<string>("Mod_Entry_Area", string.Empty);
        ifo.EntryX = root.GetFieldValue<float>("Mod_Entry_X", 0.0f);
        ifo.EntryY = root.GetFieldValue<float>("Mod_Entry_Y", 0.0f);
        ifo.EntryZ = root.GetFieldValue<float>("Mod_Entry_Z", 0.0f);
        ifo.EntryDirX = root.GetFieldValue<float>("Mod_Entry_Dir_X", 0.0f);
        ifo.EntryDirY = root.GetFieldValue<float>("Mod_Entry_Dir_Y", 1.0f);

        // Module Scripts
        ifo.OnModuleLoad = root.GetFieldValue<string>("Mod_OnModLoad", string.Empty);
        ifo.OnClientEnter = root.GetFieldValue<string>("Mod_OnClientEntr", string.Empty);
        ifo.OnClientLeave = root.GetFieldValue<string>("Mod_OnClientLeav", string.Empty);
        ifo.OnHeartbeat = root.GetFieldValue<string>("Mod_OnHeartbeat", string.Empty);
        ifo.OnAcquireItem = root.GetFieldValue<string>("Mod_OnAcquirItem", string.Empty);
        ifo.OnActivateItem = root.GetFieldValue<string>("Mod_OnActvtItem", string.Empty);
        ifo.OnUnacquireItem = root.GetFieldValue<string>("Mod_OnUnAqreItem", string.Empty);
        ifo.OnPlayerDeath = root.GetFieldValue<string>("Mod_OnPlrDeath", string.Empty);
        ifo.OnPlayerDying = root.GetFieldValue<string>("Mod_OnPlrDying", string.Empty);
        ifo.OnPlayerRest = root.GetFieldValue<string>("Mod_OnPlrRest", string.Empty);
        ifo.OnPlayerEquipItem = root.GetFieldValue<string>("Mod_OnPlrEqItm", string.Empty);
        ifo.OnPlayerUnequipItem = root.GetFieldValue<string>("Mod_OnPlrUnEqItm", string.Empty);
        ifo.OnPlayerLevelUp = root.GetFieldValue<string>("Mod_OnPlrLvlUp", string.Empty);
        ifo.OnUserDefined = root.GetFieldValue<string>("Mod_OnUsrDefined", string.Empty);
        ifo.OnSpawnButtonDown = root.GetFieldValue<string>("Mod_OnSpawnBtnDn", string.Empty);
        ifo.OnCutsceneAbort = root.GetFieldValue<string>("Mod_OnCutsnAbort", string.Empty);

        // Other Settings
        ifo.XPScale = root.GetFieldValue<byte>("Mod_XPScale", 100);
        ifo.Creator = root.GetFieldValue<string>("Mod_Creator_ID", string.Empty);
        ifo.ModuleVersion = root.GetFieldValue<uint>("Mod_Version", 0);

        // Area List
        ifo.AreaList = ReadAreaList(root);

        // Local Variables
        ifo.VarTable = VarTableHelper.ReadVarTable(root);

        return ifo;
    }

    /// <summary>
    /// Read a localized string field.
    /// </summary>
    private static CExoLocString ReadLocString(GffStruct root, string fieldName)
    {
        var field = root.GetField(fieldName);
        if (field == null)
            return new CExoLocString();

        if (field.Value is CExoLocString locString)
            return locString;

        // Handle case where it's stored as plain string
        if (field.Value is string str)
        {
            var result = new CExoLocString { StrRef = uint.MaxValue };
            result.LocalizedStrings[0] = str;
            return result;
        }

        return new CExoLocString();
    }

    /// <summary>
    /// Read the HAK list from a GFF struct.
    /// </summary>
    private static List<string> ReadHakList(GffStruct root)
    {
        var hakList = new List<string>();

        var hakListField = root.GetField("Mod_HakList");
        if (hakListField == null || !hakListField.IsList || hakListField.Value is not GffList list)
            return hakList;

        foreach (var hakStruct in list.Elements)
        {
            var hakName = hakStruct.GetFieldValue<string>("Mod_Hak", string.Empty);
            if (!string.IsNullOrEmpty(hakName))
                hakList.Add(hakName);
        }

        return hakList;
    }

    /// <summary>
    /// Read the area list from a GFF struct.
    /// </summary>
    private static List<string> ReadAreaList(GffStruct root)
    {
        var areaList = new List<string>();

        var areaListField = root.GetField("Mod_Area_List");
        if (areaListField == null || !areaListField.IsList || areaListField.Value is not GffList list)
            return areaList;

        foreach (var areaStruct in list.Elements)
        {
            var areaResRef = areaStruct.GetFieldValue<string>("Area_Name", string.Empty);
            if (!string.IsNullOrEmpty(areaResRef))
                areaList.Add(areaResRef);
        }

        return areaList;
    }
}
