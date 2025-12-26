using Radoub.Formats.Gff;
using Radoub.Formats.Uti;
using Xunit;

namespace Radoub.Formats.Tests;

public class UtiReaderTests
{
    [Fact]
    public void Read_ValidMinimalUti_ParsesCorrectly()
    {
        var buffer = CreateMinimalUtiFile();

        var uti = UtiReader.Read(buffer);

        Assert.Equal("UTI ", uti.FileType);
        Assert.Equal("V3.2", uti.FileVersion);
    }

    [Fact]
    public void Read_UtiWithCoreFields_ParsesAllFields()
    {
        var buffer = CreateUtiWithCoreFields();

        var uti = UtiReader.Read(buffer);

        Assert.Equal("test_sword", uti.TemplateResRef);
        Assert.Equal("SWORD_TAG", uti.Tag);
        Assert.Equal(4, uti.BaseItem); // Longsword
        Assert.Equal((ushort)1, uti.StackSize);
        Assert.Equal((byte)10, uti.Charges);
        Assert.Equal(500u, uti.Cost);
        Assert.Equal(100u, uti.AddCost);
        Assert.True(uti.Plot);
        Assert.False(uti.Cursed);
        Assert.False(uti.Stolen);
    }

    [Fact]
    public void Read_UtiWithLocalizedName_ParsesLocString()
    {
        var buffer = CreateUtiWithLocalizedName("Magic Sword");

        var uti = UtiReader.Read(buffer);

        Assert.False(uti.LocalizedName.IsEmpty);
        Assert.Equal("Magic Sword", uti.LocalizedName.GetDefault());
    }

    [Fact]
    public void Read_UtiWithProperties_ParsesPropertiesList()
    {
        var buffer = CreateUtiWithProperty();

        var uti = UtiReader.Read(buffer);

        Assert.Single(uti.Properties);
        var prop = uti.Properties[0];
        Assert.Equal((ushort)6, prop.PropertyName); // Enhancement Bonus
        Assert.Equal((ushort)0, prop.Subtype);
        Assert.Equal((byte)2, prop.CostTable);
        Assert.Equal((ushort)1, prop.CostValue); // +1
    }

    [Fact]
    public void Read_UtiWithModelParts_ParsesModelFields()
    {
        var buffer = CreateUtiWithModelParts();

        var uti = UtiReader.Read(buffer);

        Assert.Equal((byte)5, uti.ModelPart1);
        Assert.Equal((byte)3, uti.ModelPart2);
        Assert.Equal((byte)7, uti.ModelPart3);
    }

    [Fact]
    public void Read_UtiWithColors_ParsesColorFields()
    {
        var buffer = CreateUtiWithColors();

        var uti = UtiReader.Read(buffer);

        Assert.Equal((byte)10, uti.Cloth1Color);
        Assert.Equal((byte)20, uti.Cloth2Color);
        Assert.Equal((byte)30, uti.Leather1Color);
        Assert.Equal((byte)40, uti.Leather2Color);
        Assert.Equal((byte)50, uti.Metal1Color);
        Assert.Equal((byte)60, uti.Metal2Color);
    }

    [Fact]
    public void Read_UtiWithArmorParts_ParsesArmorPartFields()
    {
        var buffer = CreateUtiWithArmorParts();

        var uti = UtiReader.Read(buffer);

        Assert.True(uti.ArmorParts.ContainsKey("Torso"));
        Assert.True(uti.ArmorParts.ContainsKey("Belt"));
        Assert.Equal((byte)5, uti.ArmorParts["Torso"]);
        Assert.Equal((byte)2, uti.ArmorParts["Belt"]);
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var gff = CreateGffFileWithType("DLG ");
        var buffer = GffWriter.Write(gff);

        var ex = Assert.Throws<InvalidDataException>(() => UtiReader.Read(buffer));
        Assert.Contains("Invalid UTI file type", ex.Message);
    }

    [Fact]
    public void RoundTrip_MinimalUti_PreservesData()
    {
        var original = CreateMinimalUtiFile();

        var uti = UtiReader.Read(original);
        var written = UtiWriter.Write(uti);
        var uti2 = UtiReader.Read(written);

        Assert.Equal(uti.FileType, uti2.FileType);
        Assert.Equal(uti.FileVersion, uti2.FileVersion);
    }

    [Fact]
    public void RoundTrip_UtiWithCoreFields_PreservesData()
    {
        var original = CreateUtiWithCoreFields();

        var uti = UtiReader.Read(original);
        var written = UtiWriter.Write(uti);
        var uti2 = UtiReader.Read(written);

        Assert.Equal(uti.TemplateResRef, uti2.TemplateResRef);
        Assert.Equal(uti.Tag, uti2.Tag);
        Assert.Equal(uti.BaseItem, uti2.BaseItem);
        Assert.Equal(uti.Cost, uti2.Cost);
        Assert.Equal(uti.Plot, uti2.Plot);
    }

    [Fact]
    public void RoundTrip_UtiWithProperties_PreservesProperties()
    {
        var original = CreateUtiWithProperty();

        var uti = UtiReader.Read(original);
        var written = UtiWriter.Write(uti);
        var uti2 = UtiReader.Read(written);

        Assert.Equal(uti.Properties.Count, uti2.Properties.Count);
        Assert.Equal(uti.Properties[0].PropertyName, uti2.Properties[0].PropertyName);
        Assert.Equal(uti.Properties[0].CostValue, uti2.Properties[0].CostValue);
    }

    [Fact]
    public void CExoLocString_GetString_ReturnsCorrectLanguage()
    {
        var locString = new CExoLocString();
        locString.LocalizedStrings[0] = "English";
        locString.LocalizedStrings[2] = "French";

        Assert.Equal("English", locString.GetString(0));
        Assert.Equal("French", locString.GetString(2));
        Assert.Equal(string.Empty, locString.GetString(4));
    }

    [Fact]
    public void CExoLocString_GetDefault_ReturnsEnglishFirst()
    {
        var locString = new CExoLocString();
        locString.LocalizedStrings[0] = "English";
        locString.LocalizedStrings[2] = "French";

        Assert.Equal("English", locString.GetDefault());
    }

    [Fact]
    public void CExoLocString_IsEmpty_TrueWhenNoData()
    {
        var locString = new CExoLocString();

        Assert.True(locString.IsEmpty);
    }

    [Fact]
    public void ItemProperty_DefaultValues_AreCorrect()
    {
        var prop = new ItemProperty();

        Assert.Equal((byte)0xFF, prop.Param1);
        Assert.Equal((byte)100, prop.ChanceAppear);
        Assert.Equal((byte)0xFF, prop.Param2);
    }

    #region Test Helpers

    private static byte[] CreateMinimalUtiFile()
    {
        var gff = CreateGffFileWithType("UTI ");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtiWithCoreFields()
    {
        var gff = CreateGffFileWithType("UTI ");
        var root = gff.RootStruct;

        AddCResRefField(root, "TemplateResRef", "test_sword");
        AddCExoStringField(root, "Tag", "SWORD_TAG");
        AddIntField(root, "BaseItem", 4);
        AddWordField(root, "StackSize", 1);
        AddByteField(root, "Charges", 10);
        AddDwordField(root, "Cost", 500);
        AddDwordField(root, "AddCost", 100);
        AddByteField(root, "Plot", 1);
        AddByteField(root, "Cursed", 0);
        AddByteField(root, "Stolen", 0);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtiWithLocalizedName(string name)
    {
        var gff = CreateGffFileWithType("UTI ");
        var root = gff.RootStruct;

        AddCResRefField(root, "TemplateResRef", "test_item");
        AddIntField(root, "BaseItem", 0);

        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = name;
        AddLocStringField(root, "LocalizedName", locString);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtiWithProperty()
    {
        var gff = CreateGffFileWithType("UTI ");
        var root = gff.RootStruct;

        AddCResRefField(root, "TemplateResRef", "test_item");
        AddIntField(root, "BaseItem", 4);

        // Add PropertiesList with one enhancement bonus property
        var propList = new GffList();
        var propStruct = new GffStruct { Type = 0 };
        AddWordField(propStruct, "PropertyName", 6);  // Enhancement Bonus
        AddWordField(propStruct, "Subtype", 0);
        AddByteField(propStruct, "CostTable", 2);     // iprp_bonuscost
        AddWordField(propStruct, "CostValue", 1);     // +1
        AddByteField(propStruct, "Param1", 0xFF);
        AddByteField(propStruct, "Param1Value", 0);
        AddByteField(propStruct, "ChanceAppear", 100);
        AddByteField(propStruct, "Param2", 0xFF);
        AddByteField(propStruct, "Param2Value", 0);
        propList.Elements.Add(propStruct);
        propList.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "PropertiesList",
            Value = propList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtiWithModelParts()
    {
        var gff = CreateGffFileWithType("UTI ");
        var root = gff.RootStruct;

        AddCResRefField(root, "TemplateResRef", "test_item");
        AddIntField(root, "BaseItem", 0);
        AddByteField(root, "ModelPart1", 5);
        AddByteField(root, "ModelPart2", 3);
        AddByteField(root, "ModelPart3", 7);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtiWithColors()
    {
        var gff = CreateGffFileWithType("UTI ");
        var root = gff.RootStruct;

        AddCResRefField(root, "TemplateResRef", "test_item");
        AddIntField(root, "BaseItem", 0);
        AddByteField(root, "Cloth1Color", 10);
        AddByteField(root, "Cloth2Color", 20);
        AddByteField(root, "Leather1Color", 30);
        AddByteField(root, "Leather2Color", 40);
        AddByteField(root, "Metal1Color", 50);
        AddByteField(root, "Metal2Color", 60);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtiWithArmorParts()
    {
        var gff = CreateGffFileWithType("UTI ");
        var root = gff.RootStruct;

        AddCResRefField(root, "TemplateResRef", "test_armor");
        AddIntField(root, "BaseItem", 16); // Armor
        AddByteField(root, "ArmorPart_Torso", 5);
        AddByteField(root, "ArmorPart_Belt", 2);

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

    private static void AddIntField(GffStruct parent, string label, int value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.INT,
            Label = label,
            Value = value
        });
    }

    private static void AddDwordField(GffStruct parent, string label, uint value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
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
