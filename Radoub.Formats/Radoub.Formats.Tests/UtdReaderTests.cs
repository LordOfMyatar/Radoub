using Radoub.Formats.Gff;
using Radoub.Formats.Utd;
using Xunit;

namespace Radoub.Formats.Tests;

public class UtdReaderTests
{
    private static readonly string TestDataPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "TestData", "Utd");

    #region Minimal / Identity

    [Fact]
    public void Read_ValidMinimalUtd_ParsesCorrectly()
    {
        var buffer = CreateMinimalUtdFile();

        var utd = UtdReader.Read(buffer);

        Assert.Equal("UTD ", utd.FileType);
        Assert.Equal("V3.2", utd.FileVersion);
    }

    [Fact]
    public void Read_UtdWithIdentityFields_ParsesAllFields()
    {
        var buffer = CreateUtdWithIdentityFields();

        var utd = UtdReader.Read(buffer);

        Assert.Equal("door_test", utd.TemplateResRef);
        Assert.Equal("DOOR_TAG", utd.Tag);
    }

    [Fact]
    public void Read_UtdWithLocalizedName_ParsesLocString()
    {
        var buffer = CreateUtdWithLocalizedName("Iron Gate");

        var utd = UtdReader.Read(buffer);

        Assert.False(utd.LocName.IsEmpty);
        Assert.Equal("Iron Gate", utd.LocName.GetDefault());
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var gff = CreateGffFileWithType("DLG ");
        var buffer = GffWriter.Write(gff);

        var ex = Assert.Throws<InvalidDataException>(() => UtdReader.Read(buffer));
        Assert.Contains("Invalid UTD file type", ex.Message);
    }

    #endregion

    #region Combat / Physical

    [Fact]
    public void Read_UtdWithCombatFields_ParsesCorrectly()
    {
        var buffer = CreateUtdWithCombatFields();

        var utd = UtdReader.Read(buffer);

        Assert.Equal((short)30, utd.HP);
        Assert.Equal((short)30, utd.CurrentHP);
        Assert.Equal((byte)10, utd.Hardness);
        Assert.Equal((byte)3, utd.Fort);
        Assert.Equal((byte)2, utd.Ref);
        Assert.Equal((byte)1, utd.Will);
        Assert.True(utd.Plot);
    }

    [Fact]
    public void Read_UtdWithAppearance_ParsesAppearance()
    {
        var buffer = CreateUtdWithAppearance(5, 2);

        var utd = UtdReader.Read(buffer);

        Assert.Equal(5u, utd.Appearance);
        Assert.Equal((byte)2, utd.GenericType);
    }

    #endregion

    #region Lock / Trap

    [Fact]
    public void Read_UtdWithLockFields_ParsesCorrectly()
    {
        var buffer = CreateUtdWithLockFields();

        var utd = UtdReader.Read(buffer);

        Assert.True(utd.Lockable);
        Assert.True(utd.Locked);
        Assert.Equal((byte)25, utd.OpenLockDC);
        Assert.Equal((byte)20, utd.CloseLockDC);
    }

    [Fact]
    public void Read_UtdWithTrapFields_ParsesCorrectly()
    {
        var buffer = CreateUtdWithTrapFields();

        var utd = UtdReader.Read(buffer);

        Assert.True(utd.TrapFlag);
        Assert.Equal((byte)5, utd.TrapType);
        Assert.True(utd.TrapDetectable);
        Assert.Equal((byte)30, utd.TrapDetectDC);
    }

    #endregion

    #region Door-Specific

    [Fact]
    public void Read_UtdWithLinkedTo_ParsesTransitionFields()
    {
        var buffer = CreateUtdWithLinkedTo("wp_entrance", 2, 3);

        var utd = UtdReader.Read(buffer);

        Assert.Equal("wp_entrance", utd.LinkedTo);
        Assert.Equal((byte)2, utd.LinkedToFlags);
        Assert.Equal((ushort)3, utd.LoadScreenID);
    }

    #endregion

    #region Scripts

    [Fact]
    public void Read_UtdWithScripts_ParsesScriptFields()
    {
        var buffer = CreateUtdWithScripts();

        var utd = UtdReader.Read(buffer);

        Assert.Equal("nw_o2_onopen", utd.OnOpen);
        Assert.Equal("nw_o2_onclose", utd.OnClosed);
        Assert.Equal("nw_o2_ondeath", utd.OnDeath);
        Assert.Equal("nw_o2_onclick", utd.OnClick);
        Assert.Equal("nw_o2_onfail", utd.OnFailToOpen);
    }

    #endregion

    #region Metadata

    [Fact]
    public void Read_UtdWithComment_ParsesComment()
    {
        var buffer = CreateUtdWithComment("Main castle gate");

        var utd = UtdReader.Read(buffer);

        Assert.Equal("Main castle gate", utd.Comment);
    }

    [Fact]
    public void Read_UtdWithPaletteID_ParsesPaletteID()
    {
        var buffer = CreateUtdWithPaletteID(4);

        var utd = UtdReader.Read(buffer);

        Assert.Equal(4, utd.PaletteID);
    }

    #endregion

    #region Variables

    [Fact]
    public void Read_UtdWithVarTable_ParsesVariables()
    {
        var buffer = CreateUtdWithVariables();

        var utd = UtdReader.Read(buffer);

        Assert.Equal(2, utd.VarTable.Count);
        Assert.Contains(utd.VarTable, v => v.Name == "DoorState" && v.Type == VariableType.Int && v.GetInt() == 1);
        Assert.Contains(utd.VarTable, v => v.Name == "KeyTag" && v.Type == VariableType.String && v.GetString() == "castle_key");
    }

    #endregion

    #region Round-Trip

    [Fact]
    public void RoundTrip_MinimalUtd_PreservesData()
    {
        var original = CreateMinimalUtdFile();

        var utd = UtdReader.Read(original);
        var written = UtdWriter.Write(utd);
        var utd2 = UtdReader.Read(written);

        Assert.Equal(utd.FileType, utd2.FileType);
        Assert.Equal(utd.FileVersion, utd2.FileVersion);
    }

    [Fact]
    public void RoundTrip_CompleteUtd_PreservesAllFields()
    {
        var original = CreateCompleteUtd();

        var utd = UtdReader.Read(original);
        var written = UtdWriter.Write(utd);
        var utd2 = UtdReader.Read(written);

        // Identity
        Assert.Equal(utd.TemplateResRef, utd2.TemplateResRef);
        Assert.Equal(utd.Tag, utd2.Tag);
        Assert.Equal(utd.LocName.GetDefault(), utd2.LocName.GetDefault());
        Assert.Equal(utd.Description.GetDefault(), utd2.Description.GetDefault());

        // Combat
        Assert.Equal(utd.HP, utd2.HP);
        Assert.Equal(utd.CurrentHP, utd2.CurrentHP);
        Assert.Equal(utd.Hardness, utd2.Hardness);
        Assert.Equal(utd.Plot, utd2.Plot);
        Assert.Equal(utd.Appearance, utd2.Appearance);
        Assert.Equal(utd.GenericType, utd2.GenericType);

        // Lock/Trap
        Assert.Equal(utd.Lockable, utd2.Lockable);
        Assert.Equal(utd.Locked, utd2.Locked);
        Assert.Equal(utd.OpenLockDC, utd2.OpenLockDC);
        Assert.Equal(utd.TrapFlag, utd2.TrapFlag);
        Assert.Equal(utd.TrapType, utd2.TrapType);

        // Door-specific
        Assert.Equal(utd.LinkedTo, utd2.LinkedTo);
        Assert.Equal(utd.LinkedToFlags, utd2.LinkedToFlags);
        Assert.Equal(utd.LoadScreenID, utd2.LoadScreenID);

        // Scripts
        Assert.Equal(utd.OnOpen, utd2.OnOpen);
        Assert.Equal(utd.OnClosed, utd2.OnClosed);
        Assert.Equal(utd.OnClick, utd2.OnClick);
        Assert.Equal(utd.OnFailToOpen, utd2.OnFailToOpen);

        // Metadata
        Assert.Equal(utd.Comment, utd2.Comment);
        Assert.Equal(utd.PaletteID, utd2.PaletteID);
        Assert.Equal(utd.Conversation, utd2.Conversation);

        // Variables
        Assert.Equal(utd.VarTable.Count, utd2.VarTable.Count);
    }

    [Fact]
    public void RoundTrip_RealFile_DoorMet005_PreservesData()
    {
        var filePath = Path.Combine(TestDataPath, "door_met005.utd");
        if (!File.Exists(filePath)) return;

        var original = File.ReadAllBytes(filePath);
        var utd = UtdReader.Read(original);
        var written = UtdWriter.Write(utd);
        var utd2 = UtdReader.Read(written);

        Assert.Equal(utd.TemplateResRef, utd2.TemplateResRef);
        Assert.Equal(utd.Tag, utd2.Tag);
        Assert.Equal(utd.LocName.GetDefault(), utd2.LocName.GetDefault());
        Assert.Equal(utd.HP, utd2.HP);
        Assert.Equal(utd.Appearance, utd2.Appearance);
        Assert.Equal(utd.Locked, utd2.Locked);
        Assert.Equal(utd.TrapFlag, utd2.TrapFlag);
        Assert.Equal(utd.LinkedTo, utd2.LinkedTo);
        Assert.Equal(utd.LinkedToFlags, utd2.LinkedToFlags);
        Assert.Equal(utd.VarTable.Count, utd2.VarTable.Count);
    }

    [Fact]
    public void RoundTrip_RealFile_DoorWood003_PreservesData()
    {
        var filePath = Path.Combine(TestDataPath, "door_wood003.utd");
        if (!File.Exists(filePath)) return;

        var original = File.ReadAllBytes(filePath);
        var utd = UtdReader.Read(original);
        var written = UtdWriter.Write(utd);
        var utd2 = UtdReader.Read(written);

        Assert.Equal(utd.TemplateResRef, utd2.TemplateResRef);
        Assert.Equal(utd.Tag, utd2.Tag);
        Assert.Equal(utd.LocName.GetDefault(), utd2.LocName.GetDefault());
        Assert.Equal(utd.HP, utd2.HP);
        Assert.Equal(utd.Appearance, utd2.Appearance);
        Assert.Equal(utd.GenericType, utd2.GenericType);
    }

    #endregion

    #region Test Helpers

    private static byte[] CreateMinimalUtdFile()
    {
        var gff = CreateGffFileWithType("UTD ");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithIdentityFields()
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "door_test");
        AddCExoStringField(root, "Tag", "DOOR_TAG");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithLocalizedName(string name)
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");
        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = name;
        AddLocStringField(root, "LocName", locString);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithCombatFields()
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");
        AddShortField(root, "HP", 30);
        AddShortField(root, "CurrentHP", 30);
        AddByteField(root, "Hardness", 10);
        AddByteField(root, "Fort", 3);
        AddByteField(root, "Ref", 2);
        AddByteField(root, "Will", 1);
        AddByteField(root, "Plot", 1);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithAppearance(uint appearance, byte genericType)
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");
        AddDwordField(root, "Appearance", appearance);
        AddByteField(root, "GenericType", genericType);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithLockFields()
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");
        AddByteField(root, "Lockable", 1);
        AddByteField(root, "Locked", 1);
        AddByteField(root, "OpenLockDC", 25);
        AddByteField(root, "CloseLockDC", 20);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithTrapFields()
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");
        AddByteField(root, "TrapFlag", 1);
        AddByteField(root, "TrapType", 5);
        AddByteField(root, "TrapDetectable", 1);
        AddByteField(root, "TrapDetectDC", 30);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithLinkedTo(string linkedTo, byte linkedToFlags, ushort loadScreenId)
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");
        AddCExoStringField(root, "LinkedTo", linkedTo);
        AddByteField(root, "LinkedToFlags", linkedToFlags);
        AddWordField(root, "LoadScreenID", loadScreenId);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithScripts()
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");
        AddCResRefField(root, "OnOpen", "nw_o2_onopen");
        AddCResRefField(root, "OnClosed", "nw_o2_onclose");
        AddCResRefField(root, "OnDeath", "nw_o2_ondeath");
        AddCResRefField(root, "OnClick", "nw_o2_onclick");
        AddCResRefField(root, "OnFailToOpen", "nw_o2_onfail");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithComment(string comment)
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");
        AddCExoStringField(root, "Comment", comment);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithPaletteID(byte paletteId)
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");
        AddByteField(root, "PaletteID", paletteId);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtdWithVariables()
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_door");

        var varTable = new GffList();

        var intVar = new GffStruct { Type = 0 };
        AddCExoStringField(intVar, "Name", "DoorState");
        AddDwordField(intVar, "Type", 1);
        AddIntField(intVar, "Value", 1);
        varTable.Elements.Add(intVar);

        var stringVar = new GffStruct { Type = 0 };
        AddCExoStringField(stringVar, "Name", "KeyTag");
        AddDwordField(stringVar, "Type", 3);
        AddCExoStringField(stringVar, "Value", "castle_key");
        varTable.Elements.Add(stringVar);

        varTable.Count = 2;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "VarTable",
            Value = varTable
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateCompleteUtd()
    {
        var gff = CreateGffFileWithType("UTD ");
        var root = gff.RootStruct;

        // Identity
        AddCResRefField(root, "TemplateResRef", "complete_door");
        AddCExoStringField(root, "Tag", "COMPLETE_DOOR");
        var locName = new CExoLocString { StrRef = 0xFFFFFFFF };
        locName.LocalizedStrings[0] = "Complete Test Door";
        AddLocStringField(root, "LocName", locName);
        var desc = new CExoLocString { StrRef = 0xFFFFFFFF };
        desc.LocalizedStrings[0] = "A fully configured test door.";
        AddLocStringField(root, "Description", desc);

        // Appearance
        AddDwordField(root, "Appearance", 10);
        AddByteField(root, "GenericType", 3);
        AddByteField(root, "AnimationState", 0);
        AddWordField(root, "PortraitId", 0);

        // Combat
        AddShortField(root, "HP", 50);
        AddShortField(root, "CurrentHP", 50);
        AddByteField(root, "Hardness", 8);
        AddByteField(root, "Fort", 4);
        AddByteField(root, "Ref", 2);
        AddByteField(root, "Will", 1);
        AddByteField(root, "Plot", 0);
        AddDwordField(root, "Faction", 0);
        AddByteField(root, "Interruptable", 1);

        // Lock/Trap
        AddByteField(root, "Lockable", 1);
        AddByteField(root, "Locked", 1);
        AddByteField(root, "OpenLockDC", 20);
        AddByteField(root, "CloseLockDC", 15);
        AddByteField(root, "TrapFlag", 1);
        AddByteField(root, "TrapType", 2);
        AddByteField(root, "TrapDetectable", 1);
        AddByteField(root, "TrapDetectDC", 18);
        AddByteField(root, "TrapDisarmable", 1);
        AddByteField(root, "DisarmDC", 15);
        AddByteField(root, "TrapOneShot", 0);

        // Door-specific
        AddCExoStringField(root, "LinkedTo", "wp_castle_entrance");
        AddByteField(root, "LinkedToFlags", 2);
        AddWordField(root, "LoadScreenID", 5);

        // Scripts
        AddCResRefField(root, "OnOpen", "door_onopen");
        AddCResRefField(root, "OnClosed", "door_onclose");
        AddCResRefField(root, "OnClick", "door_onclick");
        AddCResRefField(root, "OnFailToOpen", "door_onfail");

        // Metadata
        AddCExoStringField(root, "Comment", "Main castle gate");
        AddByteField(root, "PaletteID", 2);
        AddCResRefField(root, "Conversation", "door_conv");

        // Variables
        var varTable = new GffList();
        var intVar = new GffStruct { Type = 0 };
        AddCExoStringField(intVar, "Name", "GateLevel");
        AddDwordField(intVar, "Type", 1);
        AddIntField(intVar, "Value", 5);
        varTable.Elements.Add(intVar);
        varTable.Count = 1;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "VarTable",
            Value = varTable
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

    private static void AddShortField(GffStruct parent, string label, short value) =>
        parent.Fields.Add(new GffField { Type = GffField.SHORT, Label = label, Value = value });

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
