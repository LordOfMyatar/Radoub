using Radoub.Formats.Are;
using Radoub.Formats.Gff;
using Xunit;

namespace Radoub.Formats.Tests;

public class AreReaderTests
{
    private static readonly string TestDataPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "TestData", "Are");

    #region Minimal / Identity

    [Fact]
    public void Read_ValidMinimalAre_ParsesCorrectly()
    {
        var buffer = CreateMinimalAreFile();

        var are = AreReader.Read(buffer);

        Assert.Equal("ARE ", are.FileType);
        Assert.Equal("V3.2", are.FileVersion);
    }

    [Fact]
    public void Read_AreWithIdentityFields_ParsesAllFields()
    {
        var buffer = CreateAreWithIdentityFields();

        var are = AreReader.Read(buffer);

        Assert.Equal("test_area", are.ResRef);
        Assert.Equal("AREA_TAG", are.Tag);
    }

    [Fact]
    public void Read_AreWithLocalizedName_ParsesLocString()
    {
        var buffer = CreateAreWithLocalizedName("Town Square");

        var are = AreReader.Read(buffer);

        Assert.False(are.Name.IsEmpty);
        Assert.Equal("Town Square", are.Name.GetDefault());
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var gff = CreateGffFileWithType("DLG ");
        var buffer = GffWriter.Write(gff);

        var ex = Assert.Throws<InvalidDataException>(() => AreReader.Read(buffer));
        Assert.Contains("Invalid ARE file type", ex.Message);
    }

    #endregion

    #region Dimensions

    [Fact]
    public void Read_AreWithDimensions_ParsesCorrectly()
    {
        var buffer = CreateAreWithDimensions(8, 6, "tno01");

        var are = AreReader.Read(buffer);

        Assert.Equal(8, are.Width);
        Assert.Equal(6, are.Height);
        Assert.Equal("tno01", are.TileSet);
    }

    #endregion

    #region Lighting

    [Fact]
    public void Read_AreWithLighting_ParsesCorrectly()
    {
        var buffer = CreateAreWithLighting();

        var are = AreReader.Read(buffer);

        Assert.Equal((byte)1, are.DayNightCycle);
        Assert.Equal((byte)3, are.LightingScheme);
        Assert.Equal(0x00FFAABBu, are.SunAmbientColor);
        Assert.Equal(0x00CCDDEEu, are.SunDiffuseColor);
        Assert.Equal((byte)1, are.SunShadows);
    }

    #endregion

    #region Weather

    [Fact]
    public void Read_AreWithWeather_ParsesCorrectly()
    {
        var buffer = CreateAreWithWeather();

        var are = AreReader.Read(buffer);

        Assert.Equal(10, are.ChanceLightning);
        Assert.Equal(30, are.ChanceRain);
        Assert.Equal(5, are.ChanceSnow);
        Assert.Equal(1, are.WindPower);
    }

    #endregion

    #region Flags / Settings

    [Fact]
    public void Read_AreWithFlags_ParsesCorrectly()
    {
        var buffer = CreateAreWithFlags(0x0001 | 0x0004); // interior + natural

        var are = AreReader.Read(buffer);

        Assert.Equal(0x0005u, are.Flags);
    }

    [Fact]
    public void Read_AreWithNoRest_ParsesCorrectly()
    {
        var buffer = CreateAreWithNoRest();

        var are = AreReader.Read(buffer);

        Assert.Equal((byte)1, are.NoRest);
    }

    #endregion

    #region Scripts

    [Fact]
    public void Read_AreWithScripts_ParsesScriptFields()
    {
        var buffer = CreateAreWithScripts();

        var are = AreReader.Read(buffer);

        Assert.Equal("area_onenter", are.OnEnter);
        Assert.Equal("area_onexit", are.OnExit);
        Assert.Equal("area_onhb", are.OnHeartbeat);
        Assert.Equal("area_onudef", are.OnUserDefined);
    }

    #endregion

    #region Tiles

    [Fact]
    public void Read_AreWithTiles_ParsesTileList()
    {
        var buffer = CreateAreWithTiles();

        var are = AreReader.Read(buffer);

        Assert.Equal(4, are.Tiles.Count);
        Assert.Equal(1, are.Tiles[0].Tile_ID);
        Assert.Equal(2, are.Tiles[0].Tile_Orientation);
        Assert.Equal(0, are.Tiles[0].Tile_Height);
        Assert.Equal((byte)5, are.Tiles[0].Tile_MainLight1);
        Assert.Equal((byte)3, are.Tiles[0].Tile_MainLight2);
    }

    #endregion

    #region Round-Trip

    [Fact]
    public void RoundTrip_MinimalAre_PreservesData()
    {
        var original = CreateMinimalAreFile();

        var are = AreReader.Read(original);
        var written = AreWriter.Write(are);
        var are2 = AreReader.Read(written);

        Assert.Equal(are.FileType, are2.FileType);
        Assert.Equal(are.FileVersion, are2.FileVersion);
    }

    [Fact]
    public void RoundTrip_CompleteAre_PreservesAllFields()
    {
        var original = CreateCompleteAre();

        var are = AreReader.Read(original);
        var written = AreWriter.Write(are);
        var are2 = AreReader.Read(written);

        // Identity
        Assert.Equal(are.ResRef, are2.ResRef);
        Assert.Equal(are.Tag, are2.Tag);
        Assert.Equal(are.Name.GetDefault(), are2.Name.GetDefault());
        Assert.Equal(are.Comments, are2.Comments);

        // Dimensions
        Assert.Equal(are.Width, are2.Width);
        Assert.Equal(are.Height, are2.Height);
        Assert.Equal(are.TileSet, are2.TileSet);

        // Lighting
        Assert.Equal(are.DayNightCycle, are2.DayNightCycle);
        Assert.Equal(are.LightingScheme, are2.LightingScheme);
        Assert.Equal(are.SunAmbientColor, are2.SunAmbientColor);
        Assert.Equal(are.SunDiffuseColor, are2.SunDiffuseColor);
        Assert.Equal(are.MoonAmbientColor, are2.MoonAmbientColor);
        Assert.Equal(are.MoonDiffuseColor, are2.MoonDiffuseColor);
        Assert.Equal(are.ShadowOpacity, are2.ShadowOpacity);

        // Weather
        Assert.Equal(are.ChanceLightning, are2.ChanceLightning);
        Assert.Equal(are.ChanceRain, are2.ChanceRain);
        Assert.Equal(are.ChanceSnow, are2.ChanceSnow);
        Assert.Equal(are.WindPower, are2.WindPower);

        // Flags
        Assert.Equal(are.Flags, are2.Flags);
        Assert.Equal(are.NoRest, are2.NoRest);
        Assert.Equal(are.PlayerVsPlayer, are2.PlayerVsPlayer);
        Assert.Equal(are.SkyBox, are2.SkyBox);

        // Scripts
        Assert.Equal(are.OnEnter, are2.OnEnter);
        Assert.Equal(are.OnExit, are2.OnExit);
        Assert.Equal(are.OnHeartbeat, are2.OnHeartbeat);

        // Tiles
        Assert.Equal(are.Tiles.Count, are2.Tiles.Count);
        for (int i = 0; i < are.Tiles.Count; i++)
        {
            Assert.Equal(are.Tiles[i].Tile_ID, are2.Tiles[i].Tile_ID);
            Assert.Equal(are.Tiles[i].Tile_Orientation, are2.Tiles[i].Tile_Orientation);
            Assert.Equal(are.Tiles[i].Tile_Height, are2.Tiles[i].Tile_Height);
            Assert.Equal(are.Tiles[i].Tile_MainLight1, are2.Tiles[i].Tile_MainLight1);
            Assert.Equal(are.Tiles[i].Tile_MainLight2, are2.Tiles[i].Tile_MainLight2);
            Assert.Equal(are.Tiles[i].Tile_SrcLight1, are2.Tiles[i].Tile_SrcLight1);
            Assert.Equal(are.Tiles[i].Tile_SrcLight2, are2.Tiles[i].Tile_SrcLight2);
        }
    }

    [Fact]
    public void RoundTrip_RealFile_Area_PreservesData()
    {
        var filePath = Path.Combine(TestDataPath, "area.are");
        if (!File.Exists(filePath)) return;

        var original = File.ReadAllBytes(filePath);
        var are = AreReader.Read(original);
        var written = AreWriter.Write(are);
        var are2 = AreReader.Read(written);

        Assert.Equal(are.ResRef, are2.ResRef);
        Assert.Equal(are.Tag, are2.Tag);
        Assert.Equal(are.Name.GetDefault(), are2.Name.GetDefault());
        Assert.Equal(are.Width, are2.Width);
        Assert.Equal(are.Height, are2.Height);
        Assert.Equal(are.TileSet, are2.TileSet);
        Assert.Equal(are.Flags, are2.Flags);
        Assert.Equal(are.Tiles.Count, are2.Tiles.Count);

        // Verify all tiles round-trip
        for (int i = 0; i < are.Tiles.Count; i++)
        {
            Assert.Equal(are.Tiles[i].Tile_ID, are2.Tiles[i].Tile_ID);
            Assert.Equal(are.Tiles[i].Tile_Orientation, are2.Tiles[i].Tile_Orientation);
            Assert.Equal(are.Tiles[i].Tile_Height, are2.Tiles[i].Tile_Height);
        }
    }

    [Fact]
    public void RoundTrip_RealFile_Admin_PreservesData()
    {
        var filePath = Path.Combine(TestDataPath, "__admin.are");
        if (!File.Exists(filePath)) return;

        var original = File.ReadAllBytes(filePath);
        var are = AreReader.Read(original);
        var written = AreWriter.Write(are);
        var are2 = AreReader.Read(written);

        Assert.Equal(are.ResRef, are2.ResRef);
        Assert.Equal(are.Tag, are2.Tag);
        Assert.Equal(are.Width, are2.Width);
        Assert.Equal(are.Height, are2.Height);
        Assert.Equal(are.Tiles.Count, are2.Tiles.Count);
        Assert.Equal(are.DayNightCycle, are2.DayNightCycle);
        Assert.Equal(are.SunAmbientColor, are2.SunAmbientColor);
        Assert.Equal(are.MoonAmbientColor, are2.MoonAmbientColor);
    }

    #endregion

    #region Test Helpers

    private static byte[] CreateMinimalAreFile()
    {
        var gff = CreateGffFileWithType("ARE ");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateAreWithIdentityFields()
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;
        AddCResRefField(root, "ResRef", "test_area");
        AddCExoStringField(root, "Tag", "AREA_TAG");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateAreWithLocalizedName(string name)
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;
        AddCResRefField(root, "ResRef", "test_area");
        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = name;
        AddLocStringField(root, "Name", locString);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateAreWithDimensions(int width, int height, string tileSet)
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;
        AddCResRefField(root, "ResRef", "test_area");
        AddIntField(root, "Width", width);
        AddIntField(root, "Height", height);
        AddCResRefField(root, "Tileset", tileSet);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateAreWithLighting()
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;
        AddCResRefField(root, "ResRef", "test_area");
        AddByteField(root, "DayNightCycle", 1);
        AddByteField(root, "LightingScheme", 3);
        AddDwordField(root, "SunAmbientColor", 0x00FFAABBu);
        AddDwordField(root, "SunDiffuseColor", 0x00CCDDEEu);
        AddByteField(root, "SunShadows", 1);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateAreWithWeather()
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;
        AddCResRefField(root, "ResRef", "test_area");
        AddIntField(root, "ChanceLightning", 10);
        AddIntField(root, "ChanceRain", 30);
        AddIntField(root, "ChanceSnow", 5);
        AddIntField(root, "WindPower", 1);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateAreWithFlags(uint flags)
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;
        AddCResRefField(root, "ResRef", "test_area");
        AddDwordField(root, "Flags", flags);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateAreWithNoRest()
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;
        AddCResRefField(root, "ResRef", "test_area");
        AddByteField(root, "NoRest", 1);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateAreWithScripts()
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;
        AddCResRefField(root, "ResRef", "test_area");
        AddCResRefField(root, "OnEnter", "area_onenter");
        AddCResRefField(root, "OnExit", "area_onexit");
        AddCResRefField(root, "OnHeartbeat", "area_onhb");
        AddCResRefField(root, "OnUserDefined", "area_onudef");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateAreWithTiles()
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;
        AddCResRefField(root, "ResRef", "test_area");
        AddIntField(root, "Width", 2);
        AddIntField(root, "Height", 2);

        var tileList = new GffList();
        for (int i = 0; i < 4; i++)
        {
            var tile = new GffStruct { Type = 1 };
            AddIntField(tile, "Tile_ID", i + 1);
            AddIntField(tile, "Tile_Orientation", i < 3 ? 2 : 0);
            AddIntField(tile, "Tile_Height", 0);
            AddByteField(tile, "Tile_MainLight1", (byte)(5 - i));
            AddByteField(tile, "Tile_MainLight2", (byte)(3 - i));
            AddByteField(tile, "Tile_SrcLight1", 0);
            AddByteField(tile, "Tile_SrcLight2", 0);
            AddIntField(tile, "Tile_AnimLoop1", 0);
            AddIntField(tile, "Tile_AnimLoop2", 0);
            AddIntField(tile, "Tile_AnimLoop3", 0);
            tileList.Elements.Add(tile);
        }
        tileList.Count = 4;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Tile_List",
            Value = tileList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateCompleteAre()
    {
        var gff = CreateGffFileWithType("ARE ");
        var root = gff.RootStruct;

        // Identity
        AddCResRefField(root, "ResRef", "complete_area");
        AddCExoStringField(root, "Tag", "COMPLETE_AREA");
        var locName = new CExoLocString { StrRef = 0xFFFFFFFF };
        locName.LocalizedStrings[0] = "Complete Test Area";
        AddLocStringField(root, "Name", locName);
        AddCExoStringField(root, "Comments", "Test area with all fields");

        // Dimensions
        AddIntField(root, "Width", 4);
        AddIntField(root, "Height", 3);
        AddCResRefField(root, "Tileset", "tno01");

        // Lighting
        AddByteField(root, "DayNightCycle", 1);
        AddByteField(root, "IsNight", 0);
        AddByteField(root, "LightingScheme", 5);
        AddDwordField(root, "SunAmbientColor", 0x00AABBCC);
        AddDwordField(root, "SunDiffuseColor", 0x00DDEEFF);
        AddByteField(root, "SunShadows", 1);
        AddByteField(root, "SunFogAmount", 2);
        AddDwordField(root, "SunFogColor", 0x00112233);
        AddDwordField(root, "MoonAmbientColor", 0x00445566);
        AddDwordField(root, "MoonDiffuseColor", 0x00778899);
        AddByteField(root, "MoonShadows", 1);
        AddByteField(root, "MoonFogAmount", 5);
        AddDwordField(root, "MoonFogColor", 0x00AABB00);
        AddByteField(root, "ShadowOpacity", 50);

        // Weather
        AddIntField(root, "ChanceLightning", 15);
        AddIntField(root, "ChanceRain", 25);
        AddIntField(root, "ChanceSnow", 0);
        AddIntField(root, "WindPower", 2);

        // Flags
        AddDwordField(root, "Flags", 0x0004); // natural
        AddWordField(root, "LoadScreenID", 3);
        AddByteField(root, "NoRest", 0);
        AddByteField(root, "PlayerVsPlayer", 1);
        AddByteField(root, "SkyBox", 2);
        AddIntField(root, "ModListenCheck", -2);
        AddIntField(root, "ModSpotCheck", 3);
        AddDwordField(root, "Version", 5);
        AddIntField(root, "Creator_ID", -1);
        AddIntField(root, "ID", -1);

        // Scripts
        AddCResRefField(root, "OnEnter", "area_enter");
        AddCResRefField(root, "OnExit", "area_exit");
        AddCResRefField(root, "OnHeartbeat", "area_hb");
        AddCResRefField(root, "OnUserDefined", "area_udef");

        // Tiles (4x3 = 12 tiles)
        var tileList = new GffList();
        for (int i = 0; i < 12; i++)
        {
            var tile = new GffStruct { Type = 1 };
            AddIntField(tile, "Tile_ID", i % 5);
            AddIntField(tile, "Tile_Orientation", i % 4);
            AddIntField(tile, "Tile_Height", i < 4 ? 0 : 1);
            AddByteField(tile, "Tile_MainLight1", (byte)(i % 8));
            AddByteField(tile, "Tile_MainLight2", (byte)(i % 4));
            AddByteField(tile, "Tile_SrcLight1", (byte)(i % 2));
            AddByteField(tile, "Tile_SrcLight2", 0);
            AddIntField(tile, "Tile_AnimLoop1", i % 2);
            AddIntField(tile, "Tile_AnimLoop2", 0);
            AddIntField(tile, "Tile_AnimLoop3", 0);
            tileList.Elements.Add(tile);
        }
        tileList.Count = 12;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Tile_List",
            Value = tileList
        });

        return GffWriter.Write(gff);
    }

    private static GffFile CreateGffFileWithType(string fileType)
    {
        return new GffFile
        {
            FileType = fileType,
            FileVersion = "V3.2",
            RootStruct = new GffStruct { Type = 0xFFFFFFFF }
        };
    }

    private static void AddByteField(GffStruct parent, string label, byte value) =>
        parent.Fields.Add(new GffField { Type = GffField.BYTE, Label = label, Value = value });

    private static void AddWordField(GffStruct parent, string label, ushort value) =>
        parent.Fields.Add(new GffField { Type = GffField.WORD, Label = label, Value = value });

    private static void AddIntField(GffStruct parent, string label, int value) =>
        parent.Fields.Add(new GffField { Type = GffField.INT, Label = label, Value = value });

    private static void AddDwordField(GffStruct parent, string label, uint value) =>
        parent.Fields.Add(new GffField { Type = GffField.DWORD, Label = label, Value = value });

    private static void AddCExoStringField(GffStruct parent, string label, string value) =>
        parent.Fields.Add(new GffField { Type = GffField.CExoString, Label = label, Value = value });

    private static void AddCResRefField(GffStruct parent, string label, string value) =>
        parent.Fields.Add(new GffField { Type = GffField.CResRef, Label = label, Value = value });

    private static void AddLocStringField(GffStruct parent, string label, CExoLocString value) =>
        parent.Fields.Add(new GffField { Type = GffField.CExoLocString, Label = label, Value = value });

    #endregion
}
