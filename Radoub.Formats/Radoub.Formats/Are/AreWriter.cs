using Radoub.Formats.Gff;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Are;

/// <summary>
/// Writes ARE (Area) files to binary format.
/// ARE files are GFF-based with file type "ARE ".
/// Reference: BioWare Aurora Area File Format specification, neverwinter.nim
/// </summary>
public static class AreWriter
{
    /// <summary>
    /// Write an ARE file to a file path.
    /// </summary>
    public static void Write(AreFile are, string filePath)
    {
        var buffer = Write(are);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write an ARE file to a stream.
    /// </summary>
    public static void Write(AreFile are, Stream stream)
    {
        var buffer = Write(are);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write an ARE file to a byte buffer.
    /// </summary>
    public static byte[] Write(AreFile are)
    {
        var gff = BuildGffFile(are);
        return GffWriter.Write(gff);
    }

    private static GffFile BuildGffFile(AreFile are)
    {
        var gff = new GffFile
        {
            FileType = are.FileType,
            FileVersion = are.FileVersion,
            RootStruct = BuildRootStruct(are)
        };

        return gff;
    }

    private static GffStruct BuildRootStruct(AreFile are)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Identity
        AddCResRefField(root, "ResRef", are.ResRef);
        AddCExoStringField(root, "Tag", are.Tag);
        AddLocStringField(root, "Name", are.Name);
        if (!string.IsNullOrEmpty(are.Comments))
            AddCExoStringField(root, "Comments", are.Comments);

        // Dimensions
        AddIntField(root, "Width", are.Width);
        AddIntField(root, "Height", are.Height);
        AddCResRefField(root, "Tileset", are.TileSet);

        // Environment / Lighting
        AddByteField(root, "DayNightCycle", are.DayNightCycle);
        AddByteField(root, "IsNight", are.IsNight);
        AddByteField(root, "LightingScheme", are.LightingScheme);
        AddDwordField(root, "SunAmbientColor", are.SunAmbientColor);
        AddDwordField(root, "SunDiffuseColor", are.SunDiffuseColor);
        AddByteField(root, "SunShadows", are.SunShadows);
        AddByteField(root, "SunFogAmount", are.SunFogAmount);
        AddDwordField(root, "SunFogColor", are.SunFogColor);
        AddDwordField(root, "MoonAmbientColor", are.MoonAmbientColor);
        AddDwordField(root, "MoonDiffuseColor", are.MoonDiffuseColor);
        AddByteField(root, "MoonShadows", are.MoonShadows);
        AddByteField(root, "MoonFogAmount", are.MoonFogAmount);
        AddDwordField(root, "MoonFogColor", are.MoonFogColor);
        AddByteField(root, "ShadowOpacity", are.ShadowOpacity);

        // Weather
        AddIntField(root, "ChanceLightning", are.ChanceLightning);
        AddIntField(root, "ChanceRain", are.ChanceRain);
        AddIntField(root, "ChanceSnow", are.ChanceSnow);
        AddIntField(root, "WindPower", are.WindPower);

        // Flags and settings
        AddDwordField(root, "Flags", are.Flags);
        AddWordField(root, "LoadScreenID", are.LoadScreenID);
        AddByteField(root, "NoRest", are.NoRest);
        AddByteField(root, "PlayerVsPlayer", are.PlayerVsPlayer);
        AddByteField(root, "SkyBox", are.SkyBox);
        AddIntField(root, "ModListenCheck", are.ModListenCheck);
        AddIntField(root, "ModSpotCheck", are.ModSpotCheck);
        AddDwordField(root, "Version", are.Version);
        AddIntField(root, "Creator_ID", are.Creator_ID);
        AddIntField(root, "ID", are.ID);

        // Scripts
        if (!string.IsNullOrEmpty(are.OnEnter))
            AddCResRefField(root, "OnEnter", are.OnEnter);
        if (!string.IsNullOrEmpty(are.OnExit))
            AddCResRefField(root, "OnExit", are.OnExit);
        if (!string.IsNullOrEmpty(are.OnHeartbeat))
            AddCResRefField(root, "OnHeartbeat", are.OnHeartbeat);
        if (!string.IsNullOrEmpty(are.OnUserDefined))
            AddCResRefField(root, "OnUserDefined", are.OnUserDefined);

        // Tile list
        AddTileList(root, are.Tiles);

        return root;
    }

    private static void AddTileList(GffStruct parent, List<AreaTile> tiles)
    {
        var list = new GffList();
        foreach (var tile in tiles)
        {
            var tileStruct = new GffStruct { Type = 1 }; // StructID 1 per spec
            AddIntField(tileStruct, "Tile_ID", tile.Tile_ID);
            AddIntField(tileStruct, "Tile_Orientation", tile.Tile_Orientation);
            AddIntField(tileStruct, "Tile_Height", tile.Tile_Height);
            AddByteField(tileStruct, "Tile_MainLight1", tile.Tile_MainLight1);
            AddByteField(tileStruct, "Tile_MainLight2", tile.Tile_MainLight2);
            AddByteField(tileStruct, "Tile_SrcLight1", tile.Tile_SrcLight1);
            AddByteField(tileStruct, "Tile_SrcLight2", tile.Tile_SrcLight2);
            AddIntField(tileStruct, "Tile_AnimLoop1", tile.Tile_AnimLoop1);
            AddIntField(tileStruct, "Tile_AnimLoop2", tile.Tile_AnimLoop2);
            AddIntField(tileStruct, "Tile_AnimLoop3", tile.Tile_AnimLoop3);
            list.Elements.Add(tileStruct);
        }
        AddListField(parent, "Tile_List", list);
    }
}
