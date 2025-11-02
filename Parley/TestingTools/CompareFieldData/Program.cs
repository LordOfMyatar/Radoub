using System;
using System.IO;
using System.Text;

class Program
{
    static void Main()
    {
        string originalFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";
        string exportedFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\__chef.dlg";

        Console.WriteLine("=== FIELDDATA SECTION COMPARISON ===\n");

        var origFieldData = ExtractFieldData(originalFile);
        var expFieldData = ExtractFieldData(exportedFile);

        Console.WriteLine($"Original FieldData: {origFieldData.Length} bytes");
        Console.WriteLine($"Exported FieldData: {expFieldData.Length} bytes");
        Console.WriteLine($"Difference: {origFieldData.Length - expFieldData.Length} bytes missing\n");

        // Find first difference
        int firstDiff = -1;
        for (int i = 0; i < Math.Min(origFieldData.Length, expFieldData.Length); i++)
        {
            if (origFieldData[i] != expFieldData[i])
            {
                firstDiff = i;
                break;
            }
        }

        if (firstDiff >= 0)
        {
            Console.WriteLine($"First difference at offset {firstDiff} (0x{firstDiff:X})");
            Console.WriteLine($"  Original byte: 0x{origFieldData[firstDiff]:X2}");
            Console.WriteLine($"  Exported byte: 0x{expFieldData[firstDiff]:X2}\n");

            // Show context around first difference
            Console.WriteLine("Context (20 bytes before/after first difference):");
            int start = Math.Max(0, firstDiff - 20);
            int end = Math.Min(origFieldData.Length, firstDiff + 20);

            Console.WriteLine("\nOriginal:");
            DumpBytes(origFieldData, start, end, firstDiff);

            Console.WriteLine("\nExported:");
            DumpBytes(expFieldData, start, Math.Min(expFieldData.Length, end), firstDiff);
        }
        else if (origFieldData.Length == expFieldData.Length)
        {
            Console.WriteLine("✅ FieldData sections are identical!");
        }
        else
        {
            Console.WriteLine("FieldData matches up to exported file length.");
            Console.WriteLine($"Missing {origFieldData.Length - expFieldData.Length} bytes at end of original.");

            // Show what's at the end of original that's missing in export
            Console.WriteLine("\nLast 100 bytes of original FieldData:");
            int showStart = Math.Max(0, origFieldData.Length - 100);
            DumpBytes(origFieldData, showStart, origFieldData.Length, -1);
        }
    }

    static byte[] ExtractFieldData(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // Skip to header offset fields
        br.BaseStream.Seek(8, SeekOrigin.Begin); // Skip signature and version

        br.ReadUInt32(); // structOffset
        br.ReadUInt32(); // structCount
        br.ReadUInt32(); // fieldOffset
        br.ReadUInt32(); // fieldCount
        br.ReadUInt32(); // labelOffset
        br.ReadUInt32(); // labelCount

        uint fieldDataOffset = br.ReadUInt32();
        uint fieldDataCount = br.ReadUInt32();

        // Seek to FieldData section and read it
        br.BaseStream.Seek(fieldDataOffset, SeekOrigin.Begin);
        return br.ReadBytes((int)fieldDataCount);
    }

    static void DumpBytes(byte[] data, int start, int end, int highlightOffset)
    {
        for (int i = start; i < end; i += 16)
        {
            Console.Write($"  {i:X4}: ");

            // Hex dump
            for (int j = 0; j < 16 && (i + j) < end; j++)
            {
                if (i + j == highlightOffset)
                    Console.Write($"[{data[i + j]:X2}] ");
                else
                    Console.Write($"{data[i + j]:X2} ");
            }

            // ASCII representation
            Console.Write("  ");
            for (int j = 0; j < 16 && (i + j) < end; j++)
            {
                byte b = data[i + j];
                char c = (b >= 32 && b < 127) ? (char)b : '.';
                Console.Write(c);
            }
            Console.WriteLine();
        }
    }
}
