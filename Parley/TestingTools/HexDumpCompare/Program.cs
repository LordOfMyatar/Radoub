using System;
using System.IO;

namespace HexDumpCompare;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: HexDumpCompare <original.dlg> <exported.dlg>");
            return;
        }

        string origPath = args[0];
        string exportPath = args[1];

        Console.WriteLine("=== GFF HEADER COMPARISON ===\n");

        byte[] origBytes = File.ReadAllBytes(origPath);
        byte[] exportBytes = File.ReadAllBytes(exportPath);

        Console.WriteLine($"Original size: {origBytes.Length} bytes");
        Console.WriteLine($"Exported size: {exportBytes.Length} bytes");
        Console.WriteLine($"Difference: {exportBytes.Length - origBytes.Length} bytes\n");

        Console.WriteLine("=== GFF HEADER (56 bytes) ===\n");

        // Signature
        Console.WriteLine($"Signature:    '{System.Text.Encoding.ASCII.GetString(origBytes, 0, 4)}' vs '{System.Text.Encoding.ASCII.GetString(exportBytes, 0, 4)}'");

        // Version
        Console.WriteLine($"Version:      '{System.Text.Encoding.ASCII.GetString(origBytes, 4, 4)}' vs '{System.Text.Encoding.ASCII.GetString(exportBytes, 4, 4)}'");

        // Offsets and counts
        PrintHeaderField("StructOffset", origBytes, exportBytes, 8);
        PrintHeaderField("StructCount", origBytes, exportBytes, 12);
        PrintHeaderField("FieldOffset", origBytes, exportBytes, 16);
        PrintHeaderField("FieldCount", origBytes, exportBytes, 20);
        PrintHeaderField("LabelOffset", origBytes, exportBytes, 24);
        PrintHeaderField("LabelCount", origBytes, exportBytes, 28);
        PrintHeaderField("FieldDataOffset", origBytes, exportBytes, 32);
        PrintHeaderField("FieldDataCount", origBytes, exportBytes, 36);
        PrintHeaderField("FieldIndicesOffset", origBytes, exportBytes, 40);
        PrintHeaderField("FieldIndicesCount", origBytes, exportBytes, 44);
        PrintHeaderField("ListIndicesOffset", origBytes, exportBytes, 48);
        PrintHeaderField("ListIndicesCount", origBytes, exportBytes, 52);

        Console.WriteLine("\n=== STRUCT ARRAY COMPARISON ===\n");

        uint origStructOffset = BitConverter.ToUInt32(origBytes, 8);
        uint origStructCount = BitConverter.ToUInt32(origBytes, 12);
        uint exportStructOffset = BitConverter.ToUInt32(exportBytes, 8);
        uint exportStructCount = BitConverter.ToUInt32(exportBytes, 12);

        for (int i = 0; i < Math.Min(origStructCount, exportStructCount); i++)
        {
            int origPos = (int)(origStructOffset + i * 12);
            int exportPos = (int)(exportStructOffset + i * 12);

            uint origType = BitConverter.ToUInt32(origBytes, origPos);
            uint origData = BitConverter.ToUInt32(origBytes, origPos + 4);
            uint origFieldCount = BitConverter.ToUInt32(origBytes, origPos + 8);

            uint exportType = BitConverter.ToUInt32(exportBytes, exportPos);
            uint exportData = BitConverter.ToUInt32(exportBytes, exportPos + 4);
            uint exportFieldCount = BitConverter.ToUInt32(exportBytes, exportPos + 8);

            if (origType != exportType || origData != exportData || origFieldCount != exportFieldCount)
            {
                Console.WriteLine($"Struct[{i}]:");
                Console.WriteLine($"  Type:   {origType} vs {exportType} {(origType != exportType ? "❌" : "✅")}");
                Console.WriteLine($"  DataOrDataOffset: {origData} vs {exportData} {(origData != exportData ? "❌" : "✅")}");
                Console.WriteLine($"  FieldCount: {origFieldCount} vs {exportFieldCount} {(origFieldCount != exportFieldCount ? "❌" : "✅")}");
            }
            else if (i < 3)
            {
                Console.WriteLine($"Struct[{i}]: Type={origType}, Data={origData}, Fields={origFieldCount} ✅");
            }
        }

        Console.WriteLine("\n=== FIELD ARRAY COMPARISON (first 20 fields) ===\n");

        uint origFieldOffset = BitConverter.ToUInt32(origBytes, 16);
        uint origFieldCountVal = BitConverter.ToUInt32(origBytes, 20);
        uint exportFieldOffset = BitConverter.ToUInt32(exportBytes, 16);
        uint exportFieldCountVal = BitConverter.ToUInt32(exportBytes, 20);

        for (int i = 0; i < Math.Min(20, Math.Min(origFieldCountVal, exportFieldCountVal)); i++)
        {
            int origPos = (int)(origFieldOffset + i * 12);
            int exportPos = (int)(exportFieldOffset + i * 12);

            uint origType = BitConverter.ToUInt32(origBytes, origPos);
            uint origLabelIdx = BitConverter.ToUInt32(origBytes, origPos + 4);
            uint origDataOff = BitConverter.ToUInt32(origBytes, origPos + 8);

            uint exportType = BitConverter.ToUInt32(exportBytes, exportPos);
            uint exportLabelIdx = BitConverter.ToUInt32(exportBytes, exportPos + 4);
            uint exportDataOff = BitConverter.ToUInt32(exportBytes, exportPos + 8);

            if (origType != exportType || origLabelIdx != exportLabelIdx || origDataOff != exportDataOff)
            {
                Console.WriteLine($"Field[{i}]:");
                Console.WriteLine($"  Type: {origType} vs {exportType} {(origType != exportType ? "❌" : "✅")}");
                Console.WriteLine($"  LabelIdx: {origLabelIdx} vs {exportLabelIdx} {(origLabelIdx != exportLabelIdx ? "❌" : "✅")}");
                Console.WriteLine($"  DataOrDataOffset: {origDataOff} vs {exportDataOff} {(origDataOff != exportDataOff ? "❌" : "✅")}");
            }
        }

        Console.WriteLine("\n=== FIELD DATA SECTION SAMPLE (first 100 bytes) ===\n");

        uint origFieldDataOffset = BitConverter.ToUInt32(origBytes, 32);
        uint exportFieldDataOffset = BitConverter.ToUInt32(exportBytes, 32);

        Console.WriteLine($"Original field data starts at offset {origFieldDataOffset}:");
        HexDump(origBytes, (int)origFieldDataOffset, 100);

        Console.WriteLine($"\nExported field data starts at offset {exportFieldDataOffset}:");
        HexDump(exportBytes, (int)exportFieldDataOffset, 100);
    }

    static void PrintHeaderField(string name, byte[] orig, byte[] export, int offset)
    {
        uint origValue = BitConverter.ToUInt32(orig, offset);
        uint exportValue = BitConverter.ToUInt32(export, offset);
        string match = origValue == exportValue ? "✅" : "❌";
        Console.WriteLine($"{name,-20}: {origValue,8} vs {exportValue,8} {match}");
    }

    static void HexDump(byte[] data, int start, int length)
    {
        int end = Math.Min(start + length, data.Length);
        for (int i = start; i < end; i += 16)
        {
            Console.Write($"{i:X4}: ");
            for (int j = 0; j < 16 && i + j < end; j++)
            {
                Console.Write($"{data[i + j]:X2} ");
            }
            Console.WriteLine();
        }
    }
}
