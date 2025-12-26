using Radoub.Formats.Gff;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Uti;

/// <summary>
/// Writes UTI (Item Blueprint) files to binary format.
/// UTI files are GFF-based with file type "UTI ".
/// Reference: BioWare Aurora Item Format specification, neverwinter.nim
/// </summary>
public static class UtiWriter
{
    /// <summary>
    /// Write a UTI file to a file path.
    /// </summary>
    public static void Write(UtiFile uti, string filePath)
    {
        var buffer = Write(uti);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write a UTI file to a stream.
    /// </summary>
    public static void Write(UtiFile uti, Stream stream)
    {
        var buffer = Write(uti);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write a UTI file to a byte buffer.
    /// </summary>
    public static byte[] Write(UtiFile uti)
    {
        var gff = BuildGffFile(uti);
        return GffWriter.Write(gff);
    }

    private static GffFile BuildGffFile(UtiFile uti)
    {
        var gff = new GffFile
        {
            FileType = uti.FileType,
            FileVersion = uti.FileVersion,
            RootStruct = BuildRootStruct(uti)
        };

        return gff;
    }

    private static GffStruct BuildRootStruct(UtiFile uti)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Core fields (Table 2.1.1)
        AddCResRefField(root, "TemplateResRef", uti.TemplateResRef);
        AddCExoStringField(root, "Tag", uti.Tag);
        AddIntField(root, "BaseItem", uti.BaseItem);
        AddWordField(root, "StackSize", uti.StackSize);
        AddByteField(root, "Charges", uti.Charges);
        AddDwordField(root, "Cost", uti.Cost);
        AddDwordField(root, "AddCost", uti.AddCost);
        AddByteField(root, "Cursed", (byte)(uti.Cursed ? 1 : 0));
        AddByteField(root, "Plot", (byte)(uti.Plot ? 1 : 0));
        AddByteField(root, "Stolen", (byte)(uti.Stolen ? 1 : 0));

        // Localized strings
        AddLocStringField(root, "LocalizedName", uti.LocalizedName);
        AddLocStringField(root, "Description", uti.Description);
        AddLocStringField(root, "DescIdentified", uti.DescIdentified);

        // Blueprint fields (Table 2.2)
        if (!string.IsNullOrEmpty(uti.Comment))
            AddCExoStringField(root, "Comment", uti.Comment);
        AddByteField(root, "PaletteID", uti.PaletteID);

        // Model part fields - only add if non-zero or if item type needs them
        if (uti.ModelPart1 != 0)
            AddByteField(root, "ModelPart1", uti.ModelPart1);
        if (uti.ModelPart2 != 0)
            AddByteField(root, "ModelPart2", uti.ModelPart2);
        if (uti.ModelPart3 != 0)
            AddByteField(root, "ModelPart3", uti.ModelPart3);

        // Color fields - only add if non-zero
        if (uti.Cloth1Color != 0)
            AddByteField(root, "Cloth1Color", uti.Cloth1Color);
        if (uti.Cloth2Color != 0)
            AddByteField(root, "Cloth2Color", uti.Cloth2Color);
        if (uti.Leather1Color != 0)
            AddByteField(root, "Leather1Color", uti.Leather1Color);
        if (uti.Leather2Color != 0)
            AddByteField(root, "Leather2Color", uti.Leather2Color);
        if (uti.Metal1Color != 0)
            AddByteField(root, "Metal1Color", uti.Metal1Color);
        if (uti.Metal2Color != 0)
            AddByteField(root, "Metal2Color", uti.Metal2Color);

        // Armor parts
        foreach (var kvp in uti.ArmorParts)
        {
            AddByteField(root, $"ArmorPart_{kvp.Key}", kvp.Value);
        }

        // Properties list
        AddPropertiesList(root, uti.Properties);

        return root;
    }

    private static void AddPropertiesList(GffStruct parent, List<ItemProperty> properties)
    {
        var list = new GffList();
        foreach (var prop in properties)
            list.Elements.Add(BuildPropertyStruct(prop));

        AddListField(parent, "PropertiesList", list);
    }

    private static GffStruct BuildPropertyStruct(ItemProperty prop)
    {
        var propStruct = new GffStruct { Type = 0 };

        // Fields from Table 2.1.3
        AddWordField(propStruct, "PropertyName", prop.PropertyName);
        AddWordField(propStruct, "Subtype", prop.Subtype);
        AddByteField(propStruct, "CostTable", prop.CostTable);
        AddWordField(propStruct, "CostValue", prop.CostValue);
        AddByteField(propStruct, "Param1", prop.Param1);
        AddByteField(propStruct, "Param1Value", prop.Param1Value);
        AddByteField(propStruct, "ChanceAppear", prop.ChanceAppear);
        AddByteField(propStruct, "Param2", prop.Param2);
        AddByteField(propStruct, "Param2Value", prop.Param2Value);

        return propStruct;
    }
}
