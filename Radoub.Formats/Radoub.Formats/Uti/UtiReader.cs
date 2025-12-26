using Radoub.Formats.Gff;

namespace Radoub.Formats.Uti;

/// <summary>
/// Reads UTI (Item Blueprint) files from binary format.
/// UTI files are GFF-based with file type "UTI ".
/// Reference: BioWare Aurora Item Format specification, neverwinter.nim
/// </summary>
public static class UtiReader
{
    /// <summary>
    /// Read a UTI file from a file path.
    /// </summary>
    public static UtiFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a UTI file from a stream.
    /// </summary>
    public static UtiFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a UTI file from a byte buffer.
    /// </summary>
    public static UtiFile Read(byte[] buffer)
    {
        // Parse as GFF first
        var gff = GffReader.Read(buffer);

        // Validate file type
        if (gff.FileType.TrimEnd() != "UTI")
        {
            throw new InvalidDataException(
                $"Invalid UTI file type: '{gff.FileType}' (expected 'UTI ')");
        }

        return ParseUtiFile(gff);
    }

    private static UtiFile ParseUtiFile(GffFile gff)
    {
        var root = gff.RootStruct;

        var uti = new UtiFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion,

            // Core fields (Table 2.1.1)
            TemplateResRef = root.GetFieldValue<string>("TemplateResRef", string.Empty),
            Tag = root.GetFieldValue<string>("Tag", string.Empty),
            BaseItem = root.GetFieldValue<int>("BaseItem", 0),
            StackSize = root.GetFieldValue<ushort>("StackSize", 1),
            Charges = root.GetFieldValue<byte>("Charges", 0),
            Cost = root.GetFieldValue<uint>("Cost", 0),
            AddCost = root.GetFieldValue<uint>("AddCost", 0),
            Cursed = root.GetFieldValue<byte>("Cursed", 0) != 0,
            Plot = root.GetFieldValue<byte>("Plot", 0) != 0,
            Stolen = root.GetFieldValue<byte>("Stolen", 0) != 0,

            // Blueprint fields (Table 2.2)
            Comment = root.GetFieldValue<string>("Comment", string.Empty),
            PaletteID = root.GetFieldValue<byte>("PaletteID", 0),

            // Model part fields (Table 2.1.2.2, 2.1.2.3)
            ModelPart1 = root.GetFieldValue<byte>("ModelPart1", 0),
            ModelPart2 = root.GetFieldValue<byte>("ModelPart2", 0),
            ModelPart3 = root.GetFieldValue<byte>("ModelPart3", 0),

            // Color fields (Table 2.1.2.1)
            Cloth1Color = root.GetFieldValue<byte>("Cloth1Color", 0),
            Cloth2Color = root.GetFieldValue<byte>("Cloth2Color", 0),
            Leather1Color = root.GetFieldValue<byte>("Leather1Color", 0),
            Leather2Color = root.GetFieldValue<byte>("Leather2Color", 0),
            Metal1Color = root.GetFieldValue<byte>("Metal1Color", 0),
            Metal2Color = root.GetFieldValue<byte>("Metal2Color", 0)
        };

        // Localized strings
        uti.LocalizedName = ParseLocString(root, "LocalizedName") ?? ParseLocString(root, "LocName") ?? new CExoLocString();
        uti.Description = ParseLocString(root, "Description") ?? new CExoLocString();
        uti.DescIdentified = ParseLocString(root, "DescIdentified") ?? new CExoLocString();

        // Armor parts (Table 2.1.2.4)
        ParseArmorParts(root, uti);

        // Properties list (Table 2.1.3)
        ParsePropertiesList(root, uti);

        return uti;
    }

    private static CExoLocString? ParseLocString(GffStruct root, string fieldName)
    {
        var field = root.GetField(fieldName);
        if (field == null || !field.IsCExoLocString || field.Value is not CExoLocString locString)
            return null;

        return locString;
    }

    private static void ParseArmorParts(GffStruct root, UtiFile uti)
    {
        // Armor part field names from Table 2.1.2.4
        string[] armorPartFields =
        {
            "ArmorPart_Belt", "ArmorPart_LBicep", "ArmorPart_RBicep",
            "ArmorPart_LFArm", "ArmorPart_RFArm", "ArmorPart_LFoot",
            "ArmorPart_RFoot", "ArmorPart_LHand", "ArmorPart_RHand",
            "ArmorPart_LShin", "ArmorPart_RShin", "ArmorPart_LShoul",
            "ArmorPart_RShoul", "ArmorPart_LThigh", "ArmorPart_RThigh",
            "ArmorPart_Neck", "ArmorPart_Pelvis", "ArmorPart_Robe",
            "ArmorPart_Torso"
        };

        foreach (var fieldName in armorPartFields)
        {
            var field = root.GetField(fieldName);
            if (field != null)
            {
                // Extract part name from field name (e.g., "ArmorPart_Belt" -> "Belt")
                var partName = fieldName.Replace("ArmorPart_", "");
                uti.ArmorParts[partName] = root.GetFieldValue<byte>(fieldName, 0);
            }
        }
    }

    private static void ParsePropertiesList(GffStruct root, UtiFile uti)
    {
        var propertiesField = root.GetField("PropertiesList");
        if (propertiesField == null || !propertiesField.IsList || propertiesField.Value is not GffList propertiesList)
            return;

        foreach (var propStruct in propertiesList.Elements)
        {
            var property = ParseItemProperty(propStruct);
            uti.Properties.Add(property);
        }
    }

    private static ItemProperty ParseItemProperty(GffStruct propStruct)
    {
        return new ItemProperty
        {
            PropertyName = propStruct.GetFieldValue<ushort>("PropertyName", 0),
            Subtype = propStruct.GetFieldValue<ushort>("Subtype", 0),
            CostTable = propStruct.GetFieldValue<byte>("CostTable", 0),
            CostValue = propStruct.GetFieldValue<ushort>("CostValue", 0),
            Param1 = propStruct.GetFieldValue<byte>("Param1", 0xFF),
            Param1Value = propStruct.GetFieldValue<byte>("Param1Value", 0),
            ChanceAppear = propStruct.GetFieldValue<byte>("ChanceAppear", 100),
            Param2 = propStruct.GetFieldValue<byte>("Param2", 0xFF),
            Param2Value = propStruct.GetFieldValue<byte>("Param2Value", 0)
        };
    }
}
