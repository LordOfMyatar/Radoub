using Radoub.Formats.Gff;
using Radoub.Formats.Utc;
using Xunit;

namespace Radoub.Formats.Tests;

public class UtcReaderTests
{
    [Fact]
    public void Read_ValidMinimalUtc_ParsesCorrectly()
    {
        var buffer = CreateMinimalUtcFile();

        var utc = UtcReader.Read(buffer);

        Assert.Equal("UTC ", utc.FileType);
        Assert.Equal("V3.2", utc.FileVersion);
    }

    [Fact]
    public void Read_UtcWithIdentityFields_ParsesAllFields()
    {
        var buffer = CreateUtcWithIdentityFields();

        var utc = UtcReader.Read(buffer);

        Assert.Equal("nw_bandit001", utc.TemplateResRef);
        Assert.Equal("BANDIT", utc.Tag);
        Assert.Equal("Designer notes here", utc.Comment);
        Assert.Equal((byte)1, utc.PaletteID);
    }

    [Fact]
    public void Read_UtcWithLocalizedNames_ParsesLocStrings()
    {
        var buffer = CreateUtcWithLocalizedNames("Goblin", "Chief");

        var utc = UtcReader.Read(buffer);

        Assert.False(utc.FirstName.IsEmpty);
        Assert.Equal("Goblin", utc.FirstName.GetDefault());
        Assert.False(utc.LastName.IsEmpty);
        Assert.Equal("Chief", utc.LastName.GetDefault());
    }

    [Fact]
    public void Read_UtcWithAbilityScores_ParsesScores()
    {
        var buffer = CreateUtcWithAbilityScores();

        var utc = UtcReader.Read(buffer);

        Assert.Equal((byte)18, utc.Str);
        Assert.Equal((byte)14, utc.Dex);
        Assert.Equal((byte)16, utc.Con);
        Assert.Equal((byte)10, utc.Int);
        Assert.Equal((byte)12, utc.Wis);
        Assert.Equal((byte)8, utc.Cha);
    }

    [Fact]
    public void Read_UtcWithHitPoints_ParsesHitPoints()
    {
        var buffer = CreateUtcWithHitPoints();

        var utc = UtcReader.Read(buffer);

        Assert.Equal((short)45, utc.HitPoints);
        Assert.Equal((short)45, utc.CurrentHitPoints);
        Assert.Equal((short)50, utc.MaxHitPoints);
    }

    [Fact]
    public void Read_UtcWithAlignment_ParsesAlignment()
    {
        var buffer = CreateUtcWithAlignment();

        var utc = UtcReader.Read(buffer);

        Assert.Equal((byte)0, utc.GoodEvil);     // Evil
        Assert.Equal((byte)100, utc.LawfulChaotic); // Lawful
    }

    [Fact]
    public void Read_UtcWithFlags_ParsesFlags()
    {
        var buffer = CreateUtcWithFlags();

        var utc = UtcReader.Read(buffer);

        Assert.True(utc.Plot);
        Assert.True(utc.IsImmortal);
        Assert.False(utc.IsPC);
        Assert.True(utc.Lootable);
    }

    [Fact]
    public void Read_UtcWithAppearance_ParsesAppearanceFields()
    {
        var buffer = CreateUtcWithAppearance();

        var utc = UtcReader.Read(buffer);

        Assert.Equal((ushort)6, utc.AppearanceType); // Human
        Assert.Equal(0, utc.Phenotype);
        Assert.Equal((ushort)100, utc.PortraitId);
        Assert.Equal((byte)0, utc.Gender);  // Male
        Assert.Equal((byte)0, utc.Race);    // Human
    }

    [Fact]
    public void Read_UtcWithClassList_ParsesClasses()
    {
        var buffer = CreateUtcWithClassList();

        var utc = UtcReader.Read(buffer);

        Assert.Equal(2, utc.ClassList.Count);
        Assert.Equal(0, utc.ClassList[0].Class);  // Fighter
        Assert.Equal((short)5, utc.ClassList[0].ClassLevel);
        Assert.Equal(1, utc.ClassList[1].Class);  // Wizard
        Assert.Equal((short)3, utc.ClassList[1].ClassLevel);
    }

    [Fact]
    public void Read_UtcWithFeatList_ParsesFeats()
    {
        var buffer = CreateUtcWithFeatList();

        var utc = UtcReader.Read(buffer);

        Assert.Equal(3, utc.FeatList.Count);
        Assert.Contains((ushort)1, utc.FeatList);   // Alertness
        Assert.Contains((ushort)4, utc.FeatList);   // Armor Proficiency (Heavy)
        Assert.Contains((ushort)45, utc.FeatList);  // Power Attack
    }

    [Fact]
    public void Read_UtcWithSkillList_ParsesSkills()
    {
        var buffer = CreateUtcWithSkillList();

        var utc = UtcReader.Read(buffer);

        Assert.Equal(5, utc.SkillList.Count);
        Assert.Equal((byte)10, utc.SkillList[0]); // First skill rank
        Assert.Equal((byte)5, utc.SkillList[1]);
        Assert.Equal((byte)0, utc.SkillList[2]);
        Assert.Equal((byte)8, utc.SkillList[3]);
        Assert.Equal((byte)3, utc.SkillList[4]);
    }

    [Fact]
    public void Read_UtcWithInventory_ParsesItemList()
    {
        var buffer = CreateUtcWithInventory();

        var utc = UtcReader.Read(buffer);

        Assert.Equal(2, utc.ItemList.Count);
        Assert.Equal("nw_it_gold001", utc.ItemList[0].InventoryRes);
        Assert.True(utc.ItemList[0].Dropable);
        Assert.Equal("nw_wswss001", utc.ItemList[1].InventoryRes);
    }

    [Fact]
    public void Read_UtcWithEquippedItems_ParsesEquipItemList()
    {
        var buffer = CreateUtcWithEquippedItems();

        var utc = UtcReader.Read(buffer);

        Assert.Equal(2, utc.EquipItemList.Count);

        var rightHand = utc.EquipItemList.FirstOrDefault(e => e.Slot == EquipmentSlots.RightHand);
        Assert.NotNull(rightHand);
        Assert.Equal("nw_wswls001", rightHand.EquipRes);

        var chest = utc.EquipItemList.FirstOrDefault(e => e.Slot == EquipmentSlots.Chest);
        Assert.NotNull(chest);
        Assert.Equal("nw_aarcl001", chest.EquipRes);
    }

    [Fact]
    public void Read_UtcWithScripts_ParsesScripts()
    {
        var buffer = CreateUtcWithScripts();

        var utc = UtcReader.Read(buffer);

        Assert.Equal("nw_c2_default5", utc.ScriptAttacked);
        Assert.Equal("nw_c2_default6", utc.ScriptDamaged);
        Assert.Equal("nw_c2_default7", utc.ScriptDeath);
        Assert.Equal("nw_c2_default4", utc.ScriptDialogue);
        Assert.Equal("nw_c2_default9", utc.ScriptSpawn);
    }

    [Fact]
    public void Read_UtcWithSpecialAbilities_ParsesSpecAbilityList()
    {
        var buffer = CreateUtcWithSpecialAbilities();

        var utc = UtcReader.Read(buffer);

        Assert.Single(utc.SpecAbilityList);
        Assert.Equal((ushort)42, utc.SpecAbilityList[0].Spell); // Fireball
        Assert.Equal((byte)5, utc.SpecAbilityList[0].SpellCasterLevel);
        Assert.Equal((byte)0x01, utc.SpecAbilityList[0].SpellFlags);
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var gff = CreateGffFileWithType("DLG ");
        var buffer = GffWriter.Write(gff);

        var ex = Assert.Throws<InvalidDataException>(() => UtcReader.Read(buffer));
        Assert.Contains("Invalid UTC file type", ex.Message);
    }

    [Fact]
    public void EquipmentSlots_GetSlotName_ReturnsCorrectNames()
    {
        Assert.Equal("Head", EquipmentSlots.GetSlotName(EquipmentSlots.Head));
        Assert.Equal("Chest", EquipmentSlots.GetSlotName(EquipmentSlots.Chest));
        Assert.Equal("Right Hand", EquipmentSlots.GetSlotName(EquipmentSlots.RightHand));
        Assert.Equal("Left Hand", EquipmentSlots.GetSlotName(EquipmentSlots.LeftHand));
        Assert.Equal("Belt", EquipmentSlots.GetSlotName(EquipmentSlots.Belt));
        Assert.Equal("Arrows", EquipmentSlots.GetSlotName(EquipmentSlots.Arrows));
    }

    [Fact]
    public void EquipmentSlots_GetSlotName_UnknownSlot_ReturnsHexValue()
    {
        var name = EquipmentSlots.GetSlotName(0x9999);
        Assert.Contains("Unknown", name);
        Assert.Contains("9999", name);
    }

    #region Test Helpers

    private static byte[] CreateMinimalUtcFile()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        // Add required ClassList with at least one class
        var classList = new GffList();
        var classStruct = new GffStruct { Type = 2 };
        AddIntField(classStruct, "Class", 0);
        AddShortField(classStruct, "ClassLevel", 1);
        classList.Elements.Add(classStruct);
        classList.Count = 1;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "ClassList",
            Value = classList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithIdentityFields()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        AddCResRefField(root, "TemplateResRef", "nw_bandit001");
        AddCExoStringField(root, "Tag", "BANDIT");
        AddCExoStringField(root, "Comment", "Designer notes here");
        AddByteField(root, "PaletteID", 1);

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithLocalizedNames(string firstName, string lastName)
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        var firstNameLoc = new CExoLocString { StrRef = 0xFFFFFFFF };
        firstNameLoc.LocalizedStrings[0] = firstName;
        AddLocStringField(root, "FirstName", firstNameLoc);

        var lastNameLoc = new CExoLocString { StrRef = 0xFFFFFFFF };
        lastNameLoc.LocalizedStrings[0] = lastName;
        AddLocStringField(root, "LastName", lastNameLoc);

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithAbilityScores()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        AddByteField(root, "Str", 18);
        AddByteField(root, "Dex", 14);
        AddByteField(root, "Con", 16);
        AddByteField(root, "Int", 10);
        AddByteField(root, "Wis", 12);
        AddByteField(root, "Cha", 8);

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithHitPoints()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        AddShortField(root, "HitPoints", 45);
        AddShortField(root, "CurrentHitPoints", 45);
        AddShortField(root, "MaxHitPoints", 50);

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithAlignment()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        AddByteField(root, "GoodEvil", 0);       // Evil
        AddByteField(root, "LawfulChaotic", 100); // Lawful

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithFlags()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        AddByteField(root, "Plot", 1);
        AddByteField(root, "IsImmortal", 1);
        AddByteField(root, "IsPC", 0);
        AddByteField(root, "Lootable", 1);

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithAppearance()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        AddWordField(root, "Appearance_Type", 6); // Human
        AddIntField(root, "Phenotype", 0);
        AddWordField(root, "PortraitId", 100);
        AddByteField(root, "Gender", 0);  // Male
        AddByteField(root, "Race", 0);    // Human

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithClassList()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        var classList = new GffList();

        // Fighter 5
        var fighterStruct = new GffStruct { Type = 2 };
        AddIntField(fighterStruct, "Class", 0);
        AddShortField(fighterStruct, "ClassLevel", 5);
        classList.Elements.Add(fighterStruct);

        // Wizard 3
        var wizardStruct = new GffStruct { Type = 2 };
        AddIntField(wizardStruct, "Class", 1);
        AddShortField(wizardStruct, "ClassLevel", 3);
        classList.Elements.Add(wizardStruct);

        classList.Count = 2;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "ClassList",
            Value = classList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithFeatList()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        var featList = new GffList();

        foreach (ushort featId in new ushort[] { 1, 4, 45 })
        {
            var featStruct = new GffStruct { Type = 1 };
            AddWordField(featStruct, "Feat", featId);
            featList.Elements.Add(featStruct);
        }

        featList.Count = 3;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "FeatList",
            Value = featList
        });

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithSkillList()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        var skillList = new GffList();

        foreach (byte rank in new byte[] { 10, 5, 0, 8, 3 })
        {
            var skillStruct = new GffStruct { Type = 0 };
            AddByteField(skillStruct, "Rank", rank);
            skillList.Elements.Add(skillStruct);
        }

        skillList.Count = 5;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "SkillList",
            Value = skillList
        });

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithInventory()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        var itemList = new GffList();

        // Gold
        var goldStruct = new GffStruct { Type = 0 };
        AddCResRefField(goldStruct, "InventoryRes", "nw_it_gold001");
        AddWordField(goldStruct, "Repos_PosX", 0);
        AddWordField(goldStruct, "Repos_PosY", 0);
        AddByteField(goldStruct, "Dropable", 1);
        AddByteField(goldStruct, "Pickpocketable", 0);
        itemList.Elements.Add(goldStruct);

        // Sword
        var swordStruct = new GffStruct { Type = 0 };
        AddCResRefField(swordStruct, "InventoryRes", "nw_wswss001");
        AddWordField(swordStruct, "Repos_PosX", 1);
        AddWordField(swordStruct, "Repos_PosY", 0);
        AddByteField(swordStruct, "Dropable", 1);
        AddByteField(swordStruct, "Pickpocketable", 0);
        itemList.Elements.Add(swordStruct);

        itemList.Count = 2;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "ItemList",
            Value = itemList
        });

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithEquippedItems()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        var equipList = new GffList();

        // Right hand - longsword (slot 0x10)
        var swordStruct = new GffStruct { Type = (uint)EquipmentSlots.RightHand };
        AddCResRefField(swordStruct, "EquipRes", "nw_wswls001");
        equipList.Elements.Add(swordStruct);

        // Chest - chainmail (slot 0x2)
        var armorStruct = new GffStruct { Type = (uint)EquipmentSlots.Chest };
        AddCResRefField(armorStruct, "EquipRes", "nw_aarcl001");
        equipList.Elements.Add(armorStruct);

        equipList.Count = 2;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Equip_ItemList",
            Value = equipList
        });

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithScripts()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ScriptAttacked", "nw_c2_default5");
        AddCResRefField(root, "ScriptDamaged", "nw_c2_default6");
        AddCResRefField(root, "ScriptDeath", "nw_c2_default7");
        AddCResRefField(root, "ScriptDialogue", "nw_c2_default4");
        AddCResRefField(root, "ScriptSpawn", "nw_c2_default9");

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtcWithSpecialAbilities()
    {
        var gff = CreateGffFileWithType("UTC ");
        var root = gff.RootStruct;

        var specAbilList = new GffList();

        var abilStruct = new GffStruct { Type = 4 };
        AddWordField(abilStruct, "Spell", 42); // Fireball
        AddByteField(abilStruct, "SpellCasterLevel", 5);
        AddByteField(abilStruct, "SpellFlags", 0x01);
        specAbilList.Elements.Add(abilStruct);

        specAbilList.Count = 1;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "SpecAbilityList",
            Value = specAbilList
        });

        AddMinimalClassList(root);

        return GffWriter.Write(gff);
    }

    private static void AddMinimalClassList(GffStruct root)
    {
        var classList = new GffList();
        var classStruct = new GffStruct { Type = 2 };
        AddIntField(classStruct, "Class", 0);
        AddShortField(classStruct, "ClassLevel", 1);
        classList.Elements.Add(classStruct);
        classList.Count = 1;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "ClassList",
            Value = classList
        });
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

    private static void AddByteField(GffStruct parent, string label, byte value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.BYTE,
            Label = label,
            Value = value
        });
    }

    private static void AddWordField(GffStruct parent, string label, ushort value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.WORD,
            Label = label,
            Value = value
        });
    }

    private static void AddShortField(GffStruct parent, string label, short value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.SHORT,
            Label = label,
            Value = value
        });
    }

    private static void AddIntField(GffStruct parent, string label, int value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.INT,
            Label = label,
            Value = value
        });
    }

    private static void AddCExoStringField(GffStruct parent, string label, string value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CExoString,
            Label = label,
            Value = value
        });
    }

    private static void AddCResRefField(GffStruct parent, string label, string value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CResRef,
            Label = label,
            Value = value
        });
    }

    private static void AddLocStringField(GffStruct parent, string label, CExoLocString value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CExoLocString,
            Label = label,
            Value = value
        });
    }

    #endregion
}
