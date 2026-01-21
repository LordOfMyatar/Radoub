using Radoub.Formats.Gff;
using Radoub.Formats.Ifo;
using Xunit;

namespace Radoub.Formats.Tests;

public class IfoReaderWriterTests
{
    #region IfoFile Model Tests

    [Fact]
    public void IfoFile_DefaultValues_AreCorrect()
    {
        var ifo = new IfoFile();

        Assert.Equal("IFO ", ifo.FileType);
        Assert.Equal("V3.2", ifo.FileVersion);
        Assert.Equal("1.69", ifo.MinGameVersion);
        Assert.Equal(6, ifo.DawnHour);
        Assert.Equal(18, ifo.DuskHour);
        Assert.Equal(2, ifo.MinutesPerHour);
        Assert.Equal(100, ifo.XPScale);
        Assert.Empty(ifo.HakList);
        Assert.Empty(ifo.AreaList);
        Assert.Empty(ifo.VarTable);
    }

    [Fact]
    public void ExpansionPackFlags_Combinations_AreCorrect()
    {
        Assert.Equal(0, (ushort)ExpansionPackFlags.None);
        Assert.Equal(1, (ushort)ExpansionPackFlags.ShadowsOfUndrentide);
        Assert.Equal(2, (ushort)ExpansionPackFlags.HordesOfTheUnderdark);
        Assert.Equal(3, (ushort)ExpansionPackFlags.Both);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_BasicMetadata_PreservesValues()
    {
        var ifo = new IfoFile
        {
            Tag = "test_module",
            MinGameVersion = "1.89",
            XPScale = 150,
            DawnHour = 5,
            DuskHour = 20
        };
        ifo.ModuleName.LocalizedStrings[0] = "Test Module";
        ifo.ModuleDescription.LocalizedStrings[0] = "A test description";

        // Write to bytes
        var bytes = IfoWriter.Write(ifo);

        // Read back
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal("test_module", ifo2.Tag);
        Assert.Equal("1.89", ifo2.MinGameVersion);
        Assert.Equal(150, ifo2.XPScale);
        Assert.Equal(5, ifo2.DawnHour);
        Assert.Equal(20, ifo2.DuskHour);
        Assert.Equal("Test Module", ifo2.ModuleName.GetString());
        Assert.Equal("A test description", ifo2.ModuleDescription.GetString());
    }

    [Fact]
    public void RoundTrip_ExpansionPack_PreservesFlags()
    {
        var ifo = new IfoFile
        {
            ExpansionPack = (ushort)(ExpansionPackFlags.ShadowsOfUndrentide | ExpansionPackFlags.HordesOfTheUnderdark)
        };

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal(3, ifo2.ExpansionPack);
    }

    [Fact]
    public void RoundTrip_HakList_PreservesOrder()
    {
        var ifo = new IfoFile();
        ifo.HakList.Add("cep2_top_v2");
        ifo.HakList.Add("cep2_skin");
        ifo.HakList.Add("cep2_core");

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal(3, ifo2.HakList.Count);
        Assert.Equal("cep2_top_v2", ifo2.HakList[0]);
        Assert.Equal("cep2_skin", ifo2.HakList[1]);
        Assert.Equal("cep2_core", ifo2.HakList[2]);
    }

    [Fact]
    public void RoundTrip_TimeSettings_PreservesValues()
    {
        var ifo = new IfoFile
        {
            DawnHour = 7,
            DuskHour = 19,
            MinutesPerHour = 10,
            StartYear = 1400,
            StartMonth = 6,
            StartDay = 15,
            StartHour = 12
        };

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal(7, ifo2.DawnHour);
        Assert.Equal(19, ifo2.DuskHour);
        Assert.Equal(10, ifo2.MinutesPerHour);
        Assert.Equal(1400u, ifo2.StartYear);
        Assert.Equal(6, ifo2.StartMonth);
        Assert.Equal(15, ifo2.StartDay);
        Assert.Equal(12, ifo2.StartHour);
    }

    [Fact]
    public void RoundTrip_EntryPoint_PreservesValues()
    {
        var ifo = new IfoFile
        {
            EntryArea = "start_area",
            EntryX = 100.5f,
            EntryY = 200.25f,
            EntryZ = 0.0f,
            EntryDirX = 0.707f,
            EntryDirY = 0.707f
        };

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal("start_area", ifo2.EntryArea);
        Assert.Equal(100.5f, ifo2.EntryX, 0.001f);
        Assert.Equal(200.25f, ifo2.EntryY, 0.001f);
        Assert.Equal(0.0f, ifo2.EntryZ, 0.001f);
        Assert.Equal(0.707f, ifo2.EntryDirX, 0.001f);
        Assert.Equal(0.707f, ifo2.EntryDirY, 0.001f);
    }

    [Fact]
    public void RoundTrip_Scripts_PreservesValues()
    {
        var ifo = new IfoFile
        {
            OnModuleLoad = "mod_onload",
            OnClientEnter = "mod_onenter",
            OnClientLeave = "mod_onleave",
            OnHeartbeat = "mod_hb",
            OnPlayerDeath = "mod_ondeath",
            OnPlayerRest = "mod_onrest"
        };

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal("mod_onload", ifo2.OnModuleLoad);
        Assert.Equal("mod_onenter", ifo2.OnClientEnter);
        Assert.Equal("mod_onleave", ifo2.OnClientLeave);
        Assert.Equal("mod_hb", ifo2.OnHeartbeat);
        Assert.Equal("mod_ondeath", ifo2.OnPlayerDeath);
        Assert.Equal("mod_onrest", ifo2.OnPlayerRest);
    }

    [Fact]
    public void RoundTrip_AreaList_PreservesValues()
    {
        var ifo = new IfoFile();
        ifo.AreaList.Add("area_start");
        ifo.AreaList.Add("area_dungeon");
        ifo.AreaList.Add("area_town");

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal(3, ifo2.AreaList.Count);
        Assert.Contains("area_start", ifo2.AreaList);
        Assert.Contains("area_dungeon", ifo2.AreaList);
        Assert.Contains("area_town", ifo2.AreaList);
    }

    [Fact]
    public void RoundTrip_Variables_PreservesValues()
    {
        var ifo = new IfoFile();
        ifo.VarTable.Add(Variable.CreateInt("ModuleLevel", 15));
        ifo.VarTable.Add(Variable.CreateFloat("DifficultyMod", 1.5f));
        ifo.VarTable.Add(Variable.CreateString("ModuleState", "Active"));

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal(3, ifo2.VarTable.Count);
        Assert.Contains(ifo2.VarTable, v => v.Name == "ModuleLevel" && v.GetInt() == 15);
        Assert.Contains(ifo2.VarTable, v => v.Name == "DifficultyMod" && Math.Abs(v.GetFloat() - 1.5f) < 0.001f);
        Assert.Contains(ifo2.VarTable, v => v.Name == "ModuleState" && v.GetString() == "Active");
    }

    [Fact]
    public void RoundTrip_CustomTlk_PreservesValue()
    {
        var ifo = new IfoFile
        {
            CustomTlk = "mymodule"
        };

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal("mymodule", ifo2.CustomTlk);
    }

    [Fact]
    public void RoundTrip_FullModule_PreservesAllFields()
    {
        var ifo = new IfoFile
        {
            Tag = "full_module",
            MinGameVersion = "1.83",
            ExpansionPack = (ushort)ExpansionPackFlags.Both,
            CustomTlk = "fullmodule",
            DawnHour = 6,
            DuskHour = 18,
            MinutesPerHour = 2,
            StartYear = 1372,
            StartMonth = 3,
            StartDay = 12,
            StartHour = 8,
            EntryArea = "entrance",
            EntryX = 50.0f,
            EntryY = 50.0f,
            EntryZ = 0.0f,
            EntryDirX = 0.0f,
            EntryDirY = 1.0f,
            OnModuleLoad = "mod_load",
            OnClientEnter = "mod_enter",
            OnHeartbeat = "mod_hb",
            XPScale = 100
        };
        ifo.ModuleName.LocalizedStrings[0] = "Full Test Module";
        ifo.ModuleDescription.LocalizedStrings[0] = "Complete module with all fields";
        ifo.HakList.Add("cep2_top");
        ifo.HakList.Add("cep2_core");
        ifo.AreaList.Add("entrance");
        ifo.AreaList.Add("dungeon1");
        ifo.VarTable.Add(Variable.CreateInt("ModuleInit", 1));

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        // Verify all fields
        Assert.Equal("full_module", ifo2.Tag);
        Assert.Equal("1.83", ifo2.MinGameVersion);
        Assert.Equal((ushort)ExpansionPackFlags.Both, ifo2.ExpansionPack);
        Assert.Equal("fullmodule", ifo2.CustomTlk);
        Assert.Equal("Full Test Module", ifo2.ModuleName.GetString());
        Assert.Equal("Complete module with all fields", ifo2.ModuleDescription.GetString());
        Assert.Equal(2, ifo2.HakList.Count);
        Assert.Equal("entrance", ifo2.EntryArea);
        Assert.Equal(2, ifo2.AreaList.Count);
        Assert.Single(ifo2.VarTable);
    }

    #endregion

    #region GFF Conversion Tests

    [Fact]
    public void ToGff_CreatesValidGffFile()
    {
        var ifo = new IfoFile
        {
            Tag = "test"
        };

        var gff = IfoWriter.ToGff(ifo);

        Assert.Equal("IFO ", gff.FileType);
        Assert.Equal("V3.2", gff.FileVersion);
        Assert.NotNull(gff.RootStruct);
    }

    [Fact]
    public void FromGff_ParsesGffFile()
    {
        // Create a minimal GFF
        var gff = new GffFile
        {
            FileType = "IFO ",
            FileVersion = "V3.2",
            RootStruct = new GffStruct { Type = uint.MaxValue }
        };
        gff.RootStruct.Fields.Add(new GffField
        {
            Type = GffField.CExoString,
            Label = "Mod_Tag",
            Value = "parsed_module"
        });

        var ifo = IfoReader.FromGff(gff);

        Assert.Equal("parsed_module", ifo.Tag);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Read_EmptyIfo_ReturnsDefaults()
    {
        var gff = new GffFile
        {
            FileType = "IFO ",
            FileVersion = "V3.2",
            RootStruct = new GffStruct { Type = uint.MaxValue }
        };

        var bytes = GffWriter.Write(gff);
        var ifo = IfoReader.Read(bytes);

        Assert.Equal("IFO ", ifo.FileType);
        Assert.Equal(string.Empty, ifo.Tag);
        Assert.Equal("1.69", ifo.MinGameVersion);
        Assert.Empty(ifo.HakList);
    }

    [Fact]
    public void Write_EmptyHakList_DoesNotWriteField()
    {
        var ifo = new IfoFile();
        ifo.HakList.Clear();

        var gff = IfoWriter.ToGff(ifo);

        // HAK list field should not be present if empty
        Assert.Null(gff.RootStruct.GetField("Mod_HakList"));
    }

    [Fact]
    public void Write_EmptyAreaList_DoesNotWriteField()
    {
        var ifo = new IfoFile();
        ifo.AreaList.Clear();

        var gff = IfoWriter.ToGff(ifo);

        // Area list field should not be present if empty
        Assert.Null(gff.RootStruct.GetField("Mod_Area_List"));
    }

    [Fact]
    public void RoundTrip_EmptyStrings_PreservesEmpty()
    {
        var ifo = new IfoFile
        {
            Tag = string.Empty,
            CustomTlk = string.Empty,
            OnModuleLoad = string.Empty
        };

        var bytes = IfoWriter.Write(ifo);
        var ifo2 = IfoReader.Read(bytes);

        Assert.Equal(string.Empty, ifo2.Tag);
        Assert.Equal(string.Empty, ifo2.CustomTlk);
        Assert.Equal(string.Empty, ifo2.OnModuleLoad);
    }

    #endregion
}
