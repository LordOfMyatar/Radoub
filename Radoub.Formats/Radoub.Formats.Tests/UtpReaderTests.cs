using Radoub.Formats.Gff;
using Radoub.Formats.Utp;
using Xunit;

namespace Radoub.Formats.Tests;

public class UtpReaderTests
{
    private static readonly string TestDataPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "TestData", "Utp");

    #region Minimal / Identity

    [Fact]
    public void Read_ValidMinimalUtp_ParsesCorrectly()
    {
        var buffer = CreateMinimalUtpFile();

        var utp = UtpReader.Read(buffer);

        Assert.Equal("UTP ", utp.FileType);
        Assert.Equal("V3.2", utp.FileVersion);
    }

    [Fact]
    public void Read_UtpWithIdentityFields_ParsesAllFields()
    {
        var buffer = CreateUtpWithIdentityFields();

        var utp = UtpReader.Read(buffer);

        Assert.Equal("chest_test", utp.TemplateResRef);
        Assert.Equal("CHEST_TAG", utp.Tag);
    }

    [Fact]
    public void Read_UtpWithLocalizedName_ParsesLocString()
    {
        var buffer = CreateUtpWithLocalizedName("Old Wooden Chest");

        var utp = UtpReader.Read(buffer);

        Assert.False(utp.LocName.IsEmpty);
        Assert.Equal("Old Wooden Chest", utp.LocName.GetDefault());
    }

    [Fact]
    public void Read_UtpWithDescription_ParsesDescription()
    {
        var buffer = CreateUtpWithDescription("A worn chest covered in dust.");

        var utp = UtpReader.Read(buffer);

        Assert.False(utp.Description.IsEmpty);
        Assert.Equal("A worn chest covered in dust.", utp.Description.GetDefault());
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var gff = CreateGffFileWithType("DLG ");
        var buffer = GffWriter.Write(gff);

        var ex = Assert.Throws<InvalidDataException>(() => UtpReader.Read(buffer));
        Assert.Contains("Invalid UTP file type", ex.Message);
    }

    #endregion

    #region Combat / Physical

    [Fact]
    public void Read_UtpWithCombatFields_ParsesCorrectly()
    {
        var buffer = CreateUtpWithCombatFields();

        var utp = UtpReader.Read(buffer);

        Assert.Equal((short)50, utp.HP);
        Assert.Equal((short)50, utp.CurrentHP);
        Assert.Equal((byte)5, utp.Hardness);
        Assert.Equal((byte)2, utp.Fort);
        Assert.Equal((byte)3, utp.Ref);
        Assert.Equal((byte)1, utp.Will);
        Assert.True(utp.Plot);
    }

    [Fact]
    public void Read_UtpWithAppearance_ParsesAppearance()
    {
        var buffer = CreateUtpWithAppearance(32);

        var utp = UtpReader.Read(buffer);

        Assert.Equal(32u, utp.Appearance);
    }

    #endregion

    #region Lock / Trap

    [Fact]
    public void Read_UtpWithLockFields_ParsesCorrectly()
    {
        var buffer = CreateUtpWithLockFields();

        var utp = UtpReader.Read(buffer);

        Assert.True(utp.Lockable);
        Assert.True(utp.Locked);
        Assert.Equal((byte)20, utp.OpenLockDC);
        Assert.Equal((byte)15, utp.CloseLockDC);
        Assert.True(utp.AutoRemoveKey);
    }

    [Fact]
    public void Read_UtpWithTrapFields_ParsesCorrectly()
    {
        var buffer = CreateUtpWithTrapFields();

        var utp = UtpReader.Read(buffer);

        Assert.True(utp.TrapFlag);
        Assert.True(utp.TrapDetectable);
        Assert.Equal((byte)25, utp.TrapDetectDC);
        Assert.True(utp.TrapDisarmable);
        Assert.Equal((byte)20, utp.DisarmDC);
        Assert.True(utp.TrapOneShot);
        Assert.Equal((byte)3, utp.TrapType);
    }

    #endregion

    #region Placeable-Specific

    [Fact]
    public void Read_UtpWithHasInventory_ParsesInventoryFlag()
    {
        var buffer = CreateUtpWithInventory();

        var utp = UtpReader.Read(buffer);

        Assert.True(utp.HasInventory);
        Assert.True(utp.Useable);
    }

    [Fact]
    public void Read_UtpWithItemList_ParsesItems()
    {
        var buffer = CreateUtpWithItemList();

        var utp = UtpReader.Read(buffer);

        Assert.Equal(2, utp.ItemList.Count);
        Assert.Equal("nw_it_gold001", utp.ItemList[0].InventoryRes);
        Assert.Equal("nw_wswls001", utp.ItemList[1].InventoryRes);
    }

    [Fact]
    public void Read_UtpWithStaticFlag_ParsesStatic()
    {
        var buffer = CreateUtpWithStaticFlag();

        var utp = UtpReader.Read(buffer);

        Assert.True(utp.Static);
    }

    [Fact]
    public void Read_UtpWithBodyBag_ParsesBodyBag()
    {
        var buffer = CreateUtpWithBodyBag(2);

        var utp = UtpReader.Read(buffer);

        Assert.Equal((byte)2, utp.BodyBag);
    }

    #endregion

    #region Scripts

    [Fact]
    public void Read_UtpWithScripts_ParsesScriptFields()
    {
        var buffer = CreateUtpWithScripts();

        var utp = UtpReader.Read(buffer);

        Assert.Equal("nw_o2_onopen", utp.OnOpen);
        Assert.Equal("nw_o2_onclose", utp.OnClosed);
        Assert.Equal("nw_o2_ondeath", utp.OnDeath);
        Assert.Equal("nw_o2_ondmgd", utp.OnDamaged);
        Assert.Equal("nw_o2_onused", utp.OnUsed);
        Assert.Equal("nw_o2_oninvd", utp.OnInvDisturbed);
        Assert.Equal("nw_o2_onheart", utp.OnHeartbeat);
    }

    #endregion

    #region Blueprint / Metadata

    [Fact]
    public void Read_UtpWithComment_ParsesComment()
    {
        var buffer = CreateUtpWithComment("Treasure chest for Act 1");

        var utp = UtpReader.Read(buffer);

        Assert.Equal("Treasure chest for Act 1", utp.Comment);
    }

    [Fact]
    public void Read_UtpWithPaletteID_ParsesPaletteID()
    {
        var buffer = CreateUtpWithPaletteID(7);

        var utp = UtpReader.Read(buffer);

        Assert.Equal(7, utp.PaletteID);
    }

    [Fact]
    public void Read_UtpWithConversation_ParsesConversation()
    {
        var buffer = CreateUtpWithConversation("chest_conv");

        var utp = UtpReader.Read(buffer);

        Assert.Equal("chest_conv", utp.Conversation);
    }

    #endregion

    #region Variables

    [Fact]
    public void Read_UtpWithVarTable_ParsesVariables()
    {
        var buffer = CreateUtpWithVariables();

        var utp = UtpReader.Read(buffer);

        Assert.Equal(2, utp.VarTable.Count);
        Assert.Contains(utp.VarTable, v => v.Name == "ChestLevel" && v.Type == VariableType.Int && v.GetInt() == 5);
        Assert.Contains(utp.VarTable, v => v.Name == "Looted" && v.Type == VariableType.String && v.GetString() == "no");
    }

    #endregion

    #region Round-Trip

    [Fact]
    public void RoundTrip_MinimalUtp_PreservesData()
    {
        var original = CreateMinimalUtpFile();

        var utp = UtpReader.Read(original);
        var written = UtpWriter.Write(utp);
        var utp2 = UtpReader.Read(written);

        Assert.Equal(utp.FileType, utp2.FileType);
        Assert.Equal(utp.FileVersion, utp2.FileVersion);
    }

    [Fact]
    public void RoundTrip_CompleteUtp_PreservesAllFields()
    {
        var original = CreateCompleteUtp();

        var utp = UtpReader.Read(original);
        var written = UtpWriter.Write(utp);
        var utp2 = UtpReader.Read(written);

        // Identity
        Assert.Equal(utp.TemplateResRef, utp2.TemplateResRef);
        Assert.Equal(utp.Tag, utp2.Tag);
        Assert.Equal(utp.LocName.GetDefault(), utp2.LocName.GetDefault());
        Assert.Equal(utp.Description.GetDefault(), utp2.Description.GetDefault());

        // Combat
        Assert.Equal(utp.HP, utp2.HP);
        Assert.Equal(utp.CurrentHP, utp2.CurrentHP);
        Assert.Equal(utp.Hardness, utp2.Hardness);
        Assert.Equal(utp.Fort, utp2.Fort);
        Assert.Equal(utp.Ref, utp2.Ref);
        Assert.Equal(utp.Will, utp2.Will);
        Assert.Equal(utp.Plot, utp2.Plot);
        Assert.Equal(utp.Appearance, utp2.Appearance);

        // Lock/Trap
        Assert.Equal(utp.Lockable, utp2.Lockable);
        Assert.Equal(utp.Locked, utp2.Locked);
        Assert.Equal(utp.OpenLockDC, utp2.OpenLockDC);
        Assert.Equal(utp.CloseLockDC, utp2.CloseLockDC);
        Assert.Equal(utp.TrapFlag, utp2.TrapFlag);
        Assert.Equal(utp.TrapType, utp2.TrapType);

        // Placeable-specific
        Assert.Equal(utp.HasInventory, utp2.HasInventory);
        Assert.Equal(utp.Useable, utp2.Useable);
        Assert.Equal(utp.Static, utp2.Static);
        Assert.Equal(utp.BodyBag, utp2.BodyBag);

        // Scripts
        Assert.Equal(utp.OnOpen, utp2.OnOpen);
        Assert.Equal(utp.OnClosed, utp2.OnClosed);
        Assert.Equal(utp.OnUsed, utp2.OnUsed);

        // Metadata
        Assert.Equal(utp.Comment, utp2.Comment);
        Assert.Equal(utp.PaletteID, utp2.PaletteID);
        Assert.Equal(utp.Conversation, utp2.Conversation);

        // Items
        Assert.Equal(utp.ItemList.Count, utp2.ItemList.Count);

        // Variables
        Assert.Equal(utp.VarTable.Count, utp2.VarTable.Count);
    }

    [Fact]
    public void RoundTrip_RealFile_Chest1_PreservesData()
    {
        var filePath = Path.Combine(TestDataPath, "chest1.utp");
        if (!File.Exists(filePath)) return;

        var original = File.ReadAllBytes(filePath);
        var utp = UtpReader.Read(original);
        var written = UtpWriter.Write(utp);
        var utp2 = UtpReader.Read(written);

        Assert.Equal(utp.TemplateResRef, utp2.TemplateResRef);
        Assert.Equal(utp.Tag, utp2.Tag);
        Assert.Equal(utp.LocName.GetDefault(), utp2.LocName.GetDefault());
        Assert.Equal(utp.HP, utp2.HP);
        Assert.Equal(utp.Appearance, utp2.Appearance);
        Assert.Equal(utp.HasInventory, utp2.HasInventory);
        Assert.Equal(utp.Locked, utp2.Locked);
        Assert.Equal(utp.TrapFlag, utp2.TrapFlag);
        Assert.Equal(utp.VarTable.Count, utp2.VarTable.Count);
    }

    [Fact]
    public void RoundTrip_RealFile_BanditTreasure_PreservesData()
    {
        var filePath = Path.Combine(TestDataPath, "bandit_treasure.utp");
        if (!File.Exists(filePath)) return;

        var original = File.ReadAllBytes(filePath);
        var utp = UtpReader.Read(original);
        var written = UtpWriter.Write(utp);
        var utp2 = UtpReader.Read(written);

        Assert.Equal(utp.TemplateResRef, utp2.TemplateResRef);
        Assert.Equal(utp.Tag, utp2.Tag);
        Assert.Equal(utp.LocName.GetDefault(), utp2.LocName.GetDefault());
        Assert.Equal(utp.HP, utp2.HP);
        Assert.Equal(utp.Appearance, utp2.Appearance);
        Assert.Equal(utp.HasInventory, utp2.HasInventory);
        Assert.Equal(utp.ItemList.Count, utp2.ItemList.Count);
        for (int i = 0; i < utp.ItemList.Count; i++)
        {
            Assert.Equal(utp.ItemList[i].InventoryRes, utp2.ItemList[i].InventoryRes);
        }
    }

    #endregion

    #region Test Helpers

    private static byte[] CreateMinimalUtpFile()
    {
        var gff = CreateGffFileWithType("UTP ");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithIdentityFields()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "chest_test");
        AddCExoStringField(root, "Tag", "CHEST_TAG");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithLocalizedName(string name)
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = name;
        AddLocStringField(root, "LocName", locString);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithDescription(string desc)
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = desc;
        AddLocStringField(root, "Description", locString);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithCombatFields()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddShortField(root, "HP", 50);
        AddShortField(root, "CurrentHP", 50);
        AddByteField(root, "Hardness", 5);
        AddByteField(root, "Fort", 2);
        AddByteField(root, "Ref", 3);
        AddByteField(root, "Will", 1);
        AddByteField(root, "Plot", 1);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithAppearance(uint appearance)
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddDwordField(root, "Appearance", appearance);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithLockFields()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddByteField(root, "Lockable", 1);
        AddByteField(root, "Locked", 1);
        AddByteField(root, "OpenLockDC", 20);
        AddByteField(root, "CloseLockDC", 15);
        AddByteField(root, "AutoRemoveKey", 1);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithTrapFields()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddByteField(root, "TrapFlag", 1);
        AddByteField(root, "TrapDetectable", 1);
        AddByteField(root, "TrapDetectDC", 25);
        AddByteField(root, "TrapDisarmable", 1);
        AddByteField(root, "DisarmDC", 20);
        AddByteField(root, "TrapOneShot", 1);
        AddByteField(root, "TrapType", 3);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithInventory()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddByteField(root, "HasInventory", 1);
        AddByteField(root, "Useable", 1);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithItemList()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddByteField(root, "HasInventory", 1);

        var itemList = new GffList();

        var item1 = new GffStruct { Type = 0 };
        AddCResRefField(item1, "InventoryRes", "nw_it_gold001");
        AddWordField(item1, "Repos_PosX", 0);
        AddWordField(item1, "Repos_PosY", 0);
        itemList.Elements.Add(item1);

        var item2 = new GffStruct { Type = 0 };
        AddCResRefField(item2, "InventoryRes", "nw_wswls001");
        AddWordField(item2, "Repos_PosX", 1);
        AddWordField(item2, "Repos_PosY", 0);
        itemList.Elements.Add(item2);

        itemList.Count = 2;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "ItemList",
            Value = itemList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithStaticFlag()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddByteField(root, "Static", 1);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithBodyBag(byte bodyBag)
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddByteField(root, "BodyBag", bodyBag);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithScripts()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddCResRefField(root, "OnOpen", "nw_o2_onopen");
        AddCResRefField(root, "OnClosed", "nw_o2_onclose");
        AddCResRefField(root, "OnDeath", "nw_o2_ondeath");
        AddCResRefField(root, "OnDamaged", "nw_o2_ondmgd");
        AddCResRefField(root, "OnUsed", "nw_o2_onused");
        AddCResRefField(root, "OnInvDisturbed", "nw_o2_oninvd");
        AddCResRefField(root, "OnHeartbeat", "nw_o2_onheart");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithComment(string comment)
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddCExoStringField(root, "Comment", comment);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithPaletteID(byte paletteId)
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddByteField(root, "PaletteID", paletteId);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithConversation(string conversation)
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");
        AddCResRefField(root, "Conversation", conversation);
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtpWithVariables()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;
        AddCResRefField(root, "TemplateResRef", "test_plc");

        var varTable = new GffList();

        var intVar = new GffStruct { Type = 0 };
        AddCExoStringField(intVar, "Name", "ChestLevel");
        AddDwordField(intVar, "Type", 1);
        AddIntField(intVar, "Value", 5);
        varTable.Elements.Add(intVar);

        var stringVar = new GffStruct { Type = 0 };
        AddCExoStringField(stringVar, "Name", "Looted");
        AddDwordField(stringVar, "Type", 3);
        AddCExoStringField(stringVar, "Value", "no");
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

    private static byte[] CreateCompleteUtp()
    {
        var gff = CreateGffFileWithType("UTP ");
        var root = gff.RootStruct;

        // Identity
        AddCResRefField(root, "TemplateResRef", "complete_chest");
        AddCExoStringField(root, "Tag", "COMPLETE_CHEST");
        var locName = new CExoLocString { StrRef = 0xFFFFFFFF };
        locName.LocalizedStrings[0] = "Complete Test Chest";
        AddLocStringField(root, "LocName", locName);
        var desc = new CExoLocString { StrRef = 0xFFFFFFFF };
        desc.LocalizedStrings[0] = "A fully configured test chest.";
        AddLocStringField(root, "Description", desc);

        // Combat
        AddShortField(root, "HP", 100);
        AddShortField(root, "CurrentHP", 100);
        AddByteField(root, "Hardness", 10);
        AddByteField(root, "Fort", 5);
        AddByteField(root, "Ref", 3);
        AddByteField(root, "Will", 2);
        AddByteField(root, "Plot", 0);
        AddDwordField(root, "Appearance", 1);

        // Lock/Trap
        AddByteField(root, "Lockable", 1);
        AddByteField(root, "Locked", 1);
        AddByteField(root, "OpenLockDC", 25);
        AddByteField(root, "CloseLockDC", 20);
        AddByteField(root, "AutoRemoveKey", 0);
        AddByteField(root, "TrapFlag", 1);
        AddByteField(root, "TrapType", 2);
        AddByteField(root, "TrapDetectable", 1);
        AddByteField(root, "TrapDetectDC", 20);
        AddByteField(root, "TrapDisarmable", 1);
        AddByteField(root, "DisarmDC", 18);
        AddByteField(root, "TrapOneShot", 0);

        // Placeable-specific
        AddByteField(root, "HasInventory", 1);
        AddByteField(root, "Useable", 1);
        AddByteField(root, "Static", 0);
        AddByteField(root, "BodyBag", 1);
        AddByteField(root, "Interruptable", 1);
        AddDwordField(root, "Faction", 0);
        AddWordField(root, "PortraitId", 0);
        AddByteField(root, "AnimationState", 0);

        // Scripts
        AddCResRefField(root, "OnOpen", "chest_onopen");
        AddCResRefField(root, "OnClosed", "chest_onclose");
        AddCResRefField(root, "OnUsed", "chest_onused");

        // Metadata
        AddCExoStringField(root, "Comment", "Complete test chest");
        AddByteField(root, "PaletteID", 3);
        AddCResRefField(root, "Conversation", "chest_conv");

        // Items
        var itemList = new GffList();
        var item = new GffStruct { Type = 0 };
        AddCResRefField(item, "InventoryRes", "nw_it_gold001");
        AddWordField(item, "Repos_PosX", 0);
        AddWordField(item, "Repos_PosY", 0);
        itemList.Elements.Add(item);
        itemList.Count = 1;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "ItemList",
            Value = itemList
        });

        // Variables
        var varTable = new GffList();
        var intVar = new GffStruct { Type = 0 };
        AddCExoStringField(intVar, "Name", "TrapLevel");
        AddDwordField(intVar, "Type", 1);
        AddIntField(intVar, "Value", 3);
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
