using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main()
    {
        string originalFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";

        using var fs = new FileStream(originalFile, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // Read header
        br.BaseStream.Seek(8, SeekOrigin.Begin);
        br.ReadUInt32(); br.ReadUInt32(); // struct offset/count
        uint fieldOffset = br.ReadUInt32();
        uint fieldCount = br.ReadUInt32();
        br.ReadUInt32(); br.ReadUInt32(); // label offset/count
        uint fieldDataOffset = br.ReadUInt32();
        uint fieldDataCount = br.ReadUInt32();

        Console.WriteLine($"Fields: {fieldCount} @ offset {fieldOffset}");
        Console.WriteLine($"FieldData: {fieldDataCount} bytes @ offset {fieldDataOffset}");

        // Read all fields and find CExoString (type 10) offsets
        br.BaseStream.Seek(fieldOffset, SeekOrigin.Begin);

        var stringOffsets = new List<uint>();

        for (uint i = 0; i < fieldCount; i++)
        {
            uint fieldType = br.ReadUInt32();
            uint labelIndex = br.ReadUInt32();
            uint dataOrOffset = br.ReadUInt32();

            if (fieldType == 10) // CExoString
            {
                stringOffsets.Add(dataOrOffset);
            }
        }

        Console.WriteLine($"\nFound {stringOffsets.Count} CExoString fields");

        // Group by offset to find most common (likely empty string)
        var offsetGroups = stringOffsets.GroupBy(x => x).OrderByDescending(g => g.Count()).ToList();

        Console.WriteLine("\n=== Most Common CExoString Offsets (Top 10) ===");
        foreach (var group in offsetGroups.Take(10))
        {
            Console.WriteLine($"  Offset {group.Key}: {group.Count()} fields");

            // Read data at this offset
            br.BaseStream.Seek(fieldDataOffset + group.Key, SeekOrigin.Begin);
            uint length = br.ReadUInt32();
            Console.WriteLine($"    Length: {length}");
            if (length == 0)
            {
                Console.WriteLine($"    ✅ EMPTY STRING");
            }
            else if (length < 100)
            {
                byte[] data = br.ReadBytes((int)length);
                string text = System.Text.Encoding.UTF8.GetString(data);
                Console.WriteLine($"    Text: '{text}'");
            }
        }
    }
}
