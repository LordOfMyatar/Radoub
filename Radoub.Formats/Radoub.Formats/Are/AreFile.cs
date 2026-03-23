using Radoub.Formats.Gff;

namespace Radoub.Formats.Are;

/// <summary>
/// Represents an ARE (Area) file used by Aurora Engine games.
/// ARE files are GFF-based and store static area properties and tile layout.
/// Reference: BioWare Aurora Area File Format specification, neverwinter.nim
/// </summary>
public class AreFile
{
    /// <summary>
    /// File type signature - should be "ARE "
    /// </summary>
    public string FileType { get; set; } = "ARE ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    // Identity fields

    /// <summary>
    /// Area resource reference (should match filename)
    /// </summary>
    public string ResRef { get; set; } = string.Empty;

    /// <summary>
    /// Area tag, used for scripting
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Localized area name
    /// </summary>
    public CExoLocString Name { get; set; } = new();

    /// <summary>
    /// Module designer comments
    /// </summary>
    public string Comments { get; set; } = string.Empty;

    // Dimensions

    /// <summary>
    /// Area width in tiles (west-east)
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Area height in tiles (north-south)
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// ResRef of the tileset (.SET) file used by the area
    /// </summary>
    public string TileSet { get; set; } = string.Empty;

    // Environment / Lighting

    /// <summary>
    /// 1 if day/night transitions occur, 0 otherwise
    /// </summary>
    public byte DayNightCycle { get; set; } = 1;

    /// <summary>
    /// 1 if area is always night (only meaningful when DayNightCycle=0)
    /// </summary>
    public byte IsNight { get; set; }

    /// <summary>
    /// Index into environment.2da
    /// </summary>
    public byte LightingScheme { get; set; }

    /// <summary>
    /// Daytime ambient light color (BGR format)
    /// </summary>
    public uint SunAmbientColor { get; set; }

    /// <summary>
    /// Daytime diffuse light color (BGR format)
    /// </summary>
    public uint SunDiffuseColor { get; set; }

    /// <summary>
    /// 1 if shadows appear during the day
    /// </summary>
    public byte SunShadows { get; set; }

    /// <summary>
    /// Daytime fog amount (0-15)
    /// </summary>
    public byte SunFogAmount { get; set; }

    /// <summary>
    /// Daytime fog color (BGR format)
    /// </summary>
    public uint SunFogColor { get; set; }

    /// <summary>
    /// Nighttime ambient light color (BGR format)
    /// </summary>
    public uint MoonAmbientColor { get; set; }

    /// <summary>
    /// Nighttime diffuse light color (BGR format)
    /// </summary>
    public uint MoonDiffuseColor { get; set; }

    /// <summary>
    /// 1 if shadows appear at night
    /// </summary>
    public byte MoonShadows { get; set; }

    /// <summary>
    /// Nighttime fog amount (0-15)
    /// </summary>
    public byte MoonFogAmount { get; set; }

    /// <summary>
    /// Nighttime fog color (BGR format)
    /// </summary>
    public uint MoonFogColor { get; set; }

    /// <summary>
    /// Shadow opacity (0-100)
    /// </summary>
    public byte ShadowOpacity { get; set; }

    // Weather

    /// <summary>
    /// Percent chance of lightning (0-100)
    /// </summary>
    public int ChanceLightning { get; set; }

    /// <summary>
    /// Percent chance of rain (0-100)
    /// </summary>
    public int ChanceRain { get; set; }

    /// <summary>
    /// Percent chance of snow (0-100)
    /// </summary>
    public int ChanceSnow { get; set; }

    /// <summary>
    /// Wind strength (0=none, 1=weak, 2=strong)
    /// </summary>
    public int WindPower { get; set; }

    // Area flags and settings

    /// <summary>
    /// Bit flags: 0x0001=interior, 0x0002=underground, 0x0004=natural
    /// </summary>
    public uint Flags { get; set; }

    /// <summary>
    /// Index into loadscreens.2da
    /// </summary>
    public ushort LoadScreenID { get; set; }

    /// <summary>
    /// 1 if resting is not allowed
    /// </summary>
    public byte NoRest { get; set; }

    /// <summary>
    /// Index into pvpsettings.2da
    /// </summary>
    public byte PlayerVsPlayer { get; set; }

    /// <summary>
    /// Index into skyboxes.2da (0=no skybox)
    /// </summary>
    public byte SkyBox { get; set; }

    /// <summary>
    /// Modifier to Listen skill checks made in area
    /// </summary>
    public int ModListenCheck { get; set; }

    /// <summary>
    /// Modifier to Spot skill checks made in area
    /// </summary>
    public int ModSpotCheck { get; set; }

    /// <summary>
    /// Revision number, increments each save
    /// </summary>
    public uint Version { get; set; } = 1;

    /// <summary>
    /// Deprecated, always -1
    /// </summary>
    public int Creator_ID { get; set; } = -1;

    /// <summary>
    /// Deprecated, always -1
    /// </summary>
    public int ID { get; set; } = -1;

    // Scripts

    /// <summary>
    /// OnEnter event script
    /// </summary>
    public string OnEnter { get; set; } = string.Empty;

    /// <summary>
    /// OnExit event script
    /// </summary>
    public string OnExit { get; set; } = string.Empty;

    /// <summary>
    /// OnHeartbeat event script
    /// </summary>
    public string OnHeartbeat { get; set; } = string.Empty;

    /// <summary>
    /// OnUserDefined event script
    /// </summary>
    public string OnUserDefined { get; set; } = string.Empty;

    // Tile list

    /// <summary>
    /// List of tiles in the area. Tile at index i is at position (i % Width, i / Width).
    /// </summary>
    public List<AreaTile> Tiles { get; set; } = new();
}

/// <summary>
/// Represents a single tile in an area.
/// </summary>
public class AreaTile
{
    /// <summary>
    /// Index into tileset's tile list
    /// </summary>
    public int Tile_ID { get; set; }

    /// <summary>
    /// Orientation: 0=normal, 1=90CCW, 2=180CCW, 3=270CCW
    /// </summary>
    public int Tile_Orientation { get; set; }

    /// <summary>
    /// Height transition level (never negative)
    /// </summary>
    public int Tile_Height { get; set; }

    /// <summary>
    /// MainLight1 color index into lightcolor.2da (0=off/absent)
    /// </summary>
    public byte Tile_MainLight1 { get; set; }

    /// <summary>
    /// MainLight2 color index into lightcolor.2da (0=off/absent)
    /// </summary>
    public byte Tile_MainLight2 { get; set; }

    /// <summary>
    /// SourceLight1 color/animation (0=off, 1-15=on)
    /// </summary>
    public byte Tile_SrcLight1 { get; set; }

    /// <summary>
    /// SourceLight2 color/animation (0=off, 1-15=on)
    /// </summary>
    public byte Tile_SrcLight2 { get; set; }

    /// <summary>
    /// AnimLoop01 on tile model (1=play, 0=stop)
    /// </summary>
    public int Tile_AnimLoop1 { get; set; }

    /// <summary>
    /// AnimLoop02 on tile model (1=play, 0=stop)
    /// </summary>
    public int Tile_AnimLoop2 { get; set; }

    /// <summary>
    /// AnimLoop03 on tile model (1=play, 0=stop)
    /// </summary>
    public int Tile_AnimLoop3 { get; set; }
}
