using Radoub.Formats.Gff;

namespace Radoub.Formats.Jrl;

/// <summary>
/// Reads JRL (Journal) files from binary format.
/// JRL files are GFF-based with file type "JRL ".
/// </summary>
public static class JrlReader
{
    /// <summary>
    /// Read a JRL file from a file path.
    /// </summary>
    public static JrlFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a JRL file from a stream.
    /// </summary>
    public static JrlFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a JRL file from a byte buffer.
    /// </summary>
    public static JrlFile Read(byte[] buffer)
    {
        // Parse as GFF first
        var gff = GffReader.Read(buffer);

        // Validate file type
        if (gff.FileType.TrimEnd() != "JRL")
        {
            throw new InvalidDataException(
                $"Invalid JRL file type: '{gff.FileType}' (expected 'JRL ')");
        }

        var jrl = new JrlFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion
        };

        // Parse Categories list from root struct
        var categoriesField = gff.RootStruct.GetField("Categories");
        if (categoriesField != null && categoriesField.IsList && categoriesField.Value is GffList categoriesList)
        {
            foreach (var categoryStruct in categoriesList.Elements)
            {
                var category = ParseCategory(categoryStruct);
                if (category != null)
                {
                    jrl.Categories.Add(category);
                }
            }
        }

        return jrl;
    }

    private static JournalCategory? ParseCategory(GffStruct categoryStruct)
    {
        var category = new JournalCategory
        {
            Tag = categoryStruct.GetFieldValue<string>("Tag", string.Empty),
            Priority = categoryStruct.GetFieldValue<uint>("Priority", 0),
            XP = categoryStruct.GetFieldValue<uint>("XP", 0),
            Comment = categoryStruct.GetFieldValue<string>("Comment", string.Empty),
            Picture = categoryStruct.GetFieldValue<string>("Picture", string.Empty)
        };

        // Parse Name (CExoLocString)
        var nameField = categoryStruct.GetField("Name");
        if (nameField != null && nameField.IsCExoLocString && nameField.Value is CExoLocString nameLocString)
        {
            category.Name = nameLocString;
        }

        // Parse EntryList
        var entryListField = categoryStruct.GetField("EntryList");
        if (entryListField != null && entryListField.IsList && entryListField.Value is GffList entryList)
        {
            foreach (var entryStruct in entryList.Elements)
            {
                var entry = ParseEntry(entryStruct);
                if (entry != null)
                {
                    category.Entries.Add(entry);
                }
            }
        }

        return category;
    }

    private static JournalEntry? ParseEntry(GffStruct entryStruct)
    {
        var entry = new JournalEntry
        {
            ID = entryStruct.GetFieldValue<uint>("ID", 0),
            End = entryStruct.GetFieldValue<ushort>("End", 0) != 0
        };

        // Parse Text (CExoLocString)
        var textField = entryStruct.GetField("Text");
        if (textField != null && textField.IsCExoLocString && textField.Value is CExoLocString textLocString)
        {
            entry.Text = textLocString;
        }

        return entry;
    }
}
