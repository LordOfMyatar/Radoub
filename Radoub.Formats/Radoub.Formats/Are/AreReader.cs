using Radoub.Formats.Gff;

namespace Radoub.Formats.Are;

/// <summary>
/// Reads ARE (Area) files from binary format.
/// ARE files are GFF-based with file type "ARE ".
/// Reference: BioWare Aurora Area File Format specification, neverwinter.nim
/// </summary>
public static class AreReader
{
    /// <summary>
    /// Read an ARE file from a file path.
    /// </summary>
    public static AreFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read an ARE file from a stream.
    /// </summary>
    public static AreFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read an ARE file from a byte buffer.
    /// </summary>
    public static AreFile Read(byte[] buffer)
    {
        var gff = GffReader.Read(buffer);

        if (gff.FileType.TrimEnd() != "ARE")
        {
            throw new InvalidDataException(
                $"Invalid ARE file type: '{gff.FileType}' (expected 'ARE ')");
        }

        return ParseAreFile(gff);
    }

    private static AreFile ParseAreFile(GffFile gff)
    {
        var root = gff.RootStruct;

        var are = new AreFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion,

            // Identity
            ResRef = root.GetFieldValue<string>("ResRef", string.Empty),
            Tag = root.GetFieldValue<string>("Tag", string.Empty),
            Comments = root.GetFieldValue<string>("Comments", string.Empty),

            // Dimensions
            Width = root.GetFieldValue<int>("Width", 0),
            Height = root.GetFieldValue<int>("Height", 0),
            TileSet = root.GetFieldValue<string>("Tileset", string.Empty),

            // Environment / Lighting
            DayNightCycle = root.GetFieldValue<byte>("DayNightCycle", 1),
            IsNight = root.GetFieldValue<byte>("IsNight", 0),
            LightingScheme = root.GetFieldValue<byte>("LightingScheme", 0),
            SunAmbientColor = root.GetFieldValue<uint>("SunAmbientColor", 0),
            SunDiffuseColor = root.GetFieldValue<uint>("SunDiffuseColor", 0),
            SunShadows = root.GetFieldValue<byte>("SunShadows", 0),
            SunFogAmount = root.GetFieldValue<byte>("SunFogAmount", 0),
            SunFogColor = root.GetFieldValue<uint>("SunFogColor", 0),
            MoonAmbientColor = root.GetFieldValue<uint>("MoonAmbientColor", 0),
            MoonDiffuseColor = root.GetFieldValue<uint>("MoonDiffuseColor", 0),
            MoonShadows = root.GetFieldValue<byte>("MoonShadows", 0),
            MoonFogAmount = root.GetFieldValue<byte>("MoonFogAmount", 0),
            MoonFogColor = root.GetFieldValue<uint>("MoonFogColor", 0),
            ShadowOpacity = root.GetFieldValue<byte>("ShadowOpacity", 0),

            // Weather
            ChanceLightning = root.GetFieldValue<int>("ChanceLightning", 0),
            ChanceRain = root.GetFieldValue<int>("ChanceRain", 0),
            ChanceSnow = root.GetFieldValue<int>("ChanceSnow", 0),
            WindPower = root.GetFieldValue<int>("WindPower", 0),

            // Flags and settings
            Flags = root.GetFieldValue<uint>("Flags", 0),
            LoadScreenID = root.GetFieldValue<ushort>("LoadScreenID", 0),
            NoRest = root.GetFieldValue<byte>("NoRest", 0),
            PlayerVsPlayer = root.GetFieldValue<byte>("PlayerVsPlayer", 0),
            SkyBox = root.GetFieldValue<byte>("SkyBox", 0),
            ModListenCheck = root.GetFieldValue<int>("ModListenCheck", 0),
            ModSpotCheck = root.GetFieldValue<int>("ModSpotCheck", 0),
            Version = root.GetFieldValue<uint>("Version", 1),
            Creator_ID = root.GetFieldValue<int>("Creator_ID", -1),
            ID = root.GetFieldValue<int>("ID", -1),

            // Scripts
            OnEnter = root.GetFieldValue<string>("OnEnter", string.Empty),
            OnExit = root.GetFieldValue<string>("OnExit", string.Empty),
            OnHeartbeat = root.GetFieldValue<string>("OnHeartbeat", string.Empty),
            OnUserDefined = root.GetFieldValue<string>("OnUserDefined", string.Empty)
        };

        // Localized name
        are.Name = ParseLocString(root, "Name") ?? new CExoLocString();

        // Tile list
        ParseTileList(root, are);

        return are;
    }

    private static CExoLocString? ParseLocString(GffStruct root, string fieldName)
    {
        var field = root.GetField(fieldName);
        if (field == null || !field.IsCExoLocString || field.Value is not CExoLocString locString)
            return null;

        return locString;
    }

    private static void ParseTileList(GffStruct root, AreFile are)
    {
        var field = root.GetField("Tile_List");
        if (field == null || !field.IsList || field.Value is not GffList tileList)
            return;

        foreach (var tileStruct in tileList.Elements)
        {
            var tile = new AreaTile
            {
                Tile_ID = tileStruct.GetFieldValue<int>("Tile_ID", 0),
                Tile_Orientation = tileStruct.GetFieldValue<int>("Tile_Orientation", 0),
                Tile_Height = tileStruct.GetFieldValue<int>("Tile_Height", 0),
                Tile_MainLight1 = tileStruct.GetFieldValue<byte>("Tile_MainLight1", 0),
                Tile_MainLight2 = tileStruct.GetFieldValue<byte>("Tile_MainLight2", 0),
                Tile_SrcLight1 = tileStruct.GetFieldValue<byte>("Tile_SrcLight1", 0),
                Tile_SrcLight2 = tileStruct.GetFieldValue<byte>("Tile_SrcLight2", 0),
                Tile_AnimLoop1 = tileStruct.GetFieldValue<int>("Tile_AnimLoop1", 0),
                Tile_AnimLoop2 = tileStruct.GetFieldValue<int>("Tile_AnimLoop2", 0),
                Tile_AnimLoop3 = tileStruct.GetFieldValue<int>("Tile_AnimLoop3", 0)
            };

            are.Tiles.Add(tile);
        }
    }
}
