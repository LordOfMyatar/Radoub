using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main()
    {
        string originalFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";
        string exportedFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\__chef.dlg";

        Console.WriteLine("=== FIELDDATA USAGE ANALYSIS ===\n");

        AnalyzeFile(originalFile, "ORIGINAL");
        Console.WriteLine();
        AnalyzeFile(exportedFile, "EXPORTED");
    }

    static void AnalyzeFile(string filePath, string label)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // Read header
        br.BaseStream.Seek(8, SeekOrigin.Begin);
        br.ReadUInt32(); br.ReadUInt32(); // struct
        uint fieldOffset = br.ReadUInt32();
        uint fieldCount = br.ReadUInt32();
        br.ReadUInt32(); br.ReadUInt32(); // label
        uint fieldDataOffset = br.ReadUInt32();
        uint fieldDataSize = br.ReadUInt32();

        Console.WriteLine($"{label}: {fieldDataSize} bytes FieldData");

        // Read all fields to find CExoString/CResRef fields
        br.BaseStream.Seek(fieldOffset, SeekOrigin.Begin);

        var stringFieldOffsets = new List<(uint offset, uint type)>();

        for (uint i = 0; i < fieldCount; i++)
        {
            uint fieldType = br.ReadUInt32();
            uint labelIndex = br.ReadUInt32();
            uint dataOrOffset = br.ReadUInt32();

            // CExoString = 10, CResRef = 8
            if (fieldType == 10 || fieldType == 8)
            {
                if (dataOrOffset > 0 && dataOrOffset < fieldDataSize)
                {
                    stringFieldOffsets.Add((dataOrOffset, fieldType));
                }
            }
        }

        // Sort by offset to find gaps
        stringFieldOffsets = stringFieldOffsets.OrderBy(x => x.offset).ToList();

        Console.WriteLine($"  String/ResRef fields pointing to FieldData: {stringFieldOffsets.Count}");

        // Read FieldData and analyze gaps
        br.BaseStream.Seek(fieldDataOffset, SeekOrigin.Begin);
        byte[] fieldData = br.ReadBytes((int)fieldDataSize);

        // Track which bytes are "used" by string data
        var usedBytes = new bool[fieldDataSize];

        foreach (var (offset, fieldType) in stringFieldOffsets)
        {
            if (offset + 4 > fieldDataSize) continue;

            uint stringLength = BitConverter.ToUInt32(fieldData, (int)offset);

            // Mark length prefix (4 bytes) as used
            for (int i = 0; i < 4; i++)
            {
                if (offset + i < fieldDataSize)
                    usedBytes[offset + i] = true;
            }

            // Mark string data as used
            for (uint i = 0; i < stringLength && offset + 4 + i < fieldDataSize; i++)
            {
                usedBytes[offset + 4 + i] = true;
            }

            // Mark padding as used (up to next 4-byte boundary)
            uint totalSize = 4 + stringLength;
            uint paddedSize = ((totalSize + 3) / 4) * 4;
            for (uint i = totalSize; i < paddedSize && offset + i < fieldDataSize; i++)
            {
                usedBytes[offset + i] = true;
            }
        }

        // Count unused bytes
        int unusedCount = usedBytes.Count(b => !b);
        Console.WriteLine($"  Used bytes: {usedBytes.Count(b => b)}");
        Console.WriteLine($"  Unused/padding bytes: {unusedCount}");

        // Find large gaps
        var gaps = new List<(int start, int length)>();
        int gapStart = -1;

        for (int i = 0; i < usedBytes.Length; i++)
        {
            if (!usedBytes[i])
            {
                if (gapStart == -1) gapStart = i;
            }
            else
            {
                if (gapStart != -1)
                {
                    gaps.Add((gapStart, i - gapStart));
                    gapStart = -1;
                }
            }
        }
        if (gapStart != -1)
        {
            gaps.Add((gapStart, usedBytes.Length - gapStart));
        }

        var largeGaps = gaps.Where(g => g.length >= 8).ToList();
        if (largeGaps.Any())
        {
            Console.WriteLine($"\n  Large gaps (>= 8 bytes): {largeGaps.Count}");
            foreach (var (start, length) in largeGaps.Take(10))
            {
                Console.WriteLine($"    Offset {start} (0x{start:X}): {length} bytes");
            }
        }
    }
}
