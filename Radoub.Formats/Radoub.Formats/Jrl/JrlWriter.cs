using Radoub.Formats.Gff;

namespace Radoub.Formats.Jrl;

/// <summary>
/// Writes JRL (Journal) files to binary format.
/// JRL files are GFF-based with file type "JRL ".
/// </summary>
public static class JrlWriter
{
    /// <summary>Write a JRL file to a file path.</summary>
    public static void Write(JrlFile jrl, string filePath)
    {
        var buffer = Write(jrl);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>Write a JRL file to a stream.</summary>
    public static void Write(JrlFile jrl, Stream stream)
    {
        var buffer = Write(jrl);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>Write a JRL file to a byte buffer.</summary>
    public static byte[] Write(JrlFile jrl)
    {
        var gff = new GffFile
        {
            FileType = jrl.FileType.PadRight(4).Substring(0, 4),
            FileVersion = jrl.FileVersion.PadRight(4).Substring(0, 4)
        };

        // Root struct (type 0xFFFFFFFF for root)
        gff.RootStruct = new GffStruct { Type = 0xFFFFFFFF };

        var categoriesList = new GffList();
        foreach (var category in jrl.Categories)
        {
            var categoryStruct = BuildCategoryStruct(category);
            categoriesList.Elements.Add(categoryStruct);
        }
        categoriesList.Count = (uint)categoriesList.Elements.Count;

        gff.RootStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "Categories",
            Value = categoriesList
        });

        return GffWriter.Write(gff);
    }

    private static GffStruct BuildCategoryStruct(JournalCategory category)
    {
        var categoryStruct = new GffStruct { Type = 0 };

        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.CExoString,
            Label = "Tag",
            Value = category.Tag
        });

        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.CExoLocString,
            Label = "Name",
            Value = category.Name
        });

        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = "Priority",
            Value = category.Priority
        });

        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = "XP",
            Value = category.XP
        });

        // Omitted from the struct entirely when empty
        if (!string.IsNullOrEmpty(category.Comment))
        {
            categoryStruct.Fields.Add(new GffField
            {
                Type = GffField.CExoString,
                Label = "Comment",
                Value = category.Comment
            });
        }

        // Omitted from the struct entirely when empty
        if (!string.IsNullOrEmpty(category.Picture))
        {
            categoryStruct.Fields.Add(new GffField
            {
                Type = GffField.CResRef,
                Label = "Picture",
                Value = category.Picture
            });
        }

        var entryList = new GffList();
        foreach (var entry in category.Entries)
        {
            var entryStruct = BuildEntryStruct(entry);
            entryList.Elements.Add(entryStruct);
        }
        entryList.Count = (uint)entryList.Elements.Count;

        categoryStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "EntryList",
            Value = entryList
        });

        return categoryStruct;
    }

    private static GffStruct BuildEntryStruct(JournalEntry entry)
    {
        var entryStruct = new GffStruct { Type = 0 };

        entryStruct.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = "ID",
            Value = entry.ID
        });

        entryStruct.Fields.Add(new GffField
        {
            Type = GffField.CExoLocString,
            Label = "Text",
            Value = entry.Text
        });

        // End (WORD - 0 or 1)
        entryStruct.Fields.Add(new GffField
        {
            Type = GffField.WORD,
            Label = "End",
            Value = (ushort)(entry.End ? 1 : 0)
        });

        return entryStruct;
    }
}
