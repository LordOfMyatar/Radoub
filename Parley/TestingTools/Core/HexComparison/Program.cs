using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string originalFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\hicks_hudson.dlg";
        string exportFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\hh_test.dlg";

        Console.WriteLine("=== HEX COMPARISON ANALYSIS ===");
        Console.WriteLine("Looking for padding, alignment, and structural differences...");
        Console.WriteLine();

        try
        {
            var originalBytes = File.ReadAllBytes(originalFile);
            var exportBytes = File.ReadAllBytes(exportFile);

            Console.WriteLine($"Original: {originalBytes.Length:N0} bytes");
            Console.WriteLine($"Export:   {exportBytes.Length:N0} bytes");
            Console.WriteLine($"Diff:     +{exportBytes.Length - originalBytes.Length:N0} bytes");
            Console.WriteLine();

            // Compare GFF headers (first 56 bytes)
            Console.WriteLine("=== GFF HEADER COMPARISON ===");
            for (int i = 0; i < Math.Min(56, Math.Min(originalBytes.Length, exportBytes.Length)); i += 4)
            {
                var origValue = BitConverter.ToUInt32(originalBytes, i);
                var expValue = BitConverter.ToUInt32(exportBytes, i);

                string fieldName = GetHeaderFieldName(i);

                if (origValue != expValue)
                {
                    Console.WriteLine($"❌ Offset {i:X2}: {fieldName}");
                    Console.WriteLine($"   Original: {origValue:N0} (0x{origValue:X8})");
                    Console.WriteLine($"   Export:   {expValue:N0} (0x{expValue:X8})");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine($"✅ Offset {i:X2}: {fieldName} = {origValue:N0}");
                }
            }

            // Look for padding patterns
            Console.WriteLine();
            Console.WriteLine("=== PADDING ANALYSIS ===");
            AnalyzePadding("Original", originalBytes);
            AnalyzePadding("Export", exportBytes);

            // Show first bytes where files differ
            Console.WriteLine();
            Console.WriteLine("=== FIRST DIFFERENCE ===");
            for (int i = 0; i < Math.Min(originalBytes.Length, exportBytes.Length); i++)
            {
                if (originalBytes[i] != exportBytes[i])
                {
                    Console.WriteLine($"First difference at byte {i:N0} (0x{i:X8}):");
                    Console.WriteLine($"  Original: 0x{originalBytes[i]:X2}");
                    Console.WriteLine($"  Export:   0x{exportBytes[i]:X2}");

                    // Show context around difference
                    int start = Math.Max(0, i - 8);
                    int end = Math.Min(originalBytes.Length, i + 8);

                    Console.WriteLine($"  Context (Original): {BitConverter.ToString(originalBytes, start, end - start)}");
                    Console.WriteLine($"  Context (Export):   {BitConverter.ToString(exportBytes, start, Math.Min(exportBytes.Length, end) - start)}");
                    break;
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    static string GetHeaderFieldName(int offset)
    {
        return offset switch
        {
            0 => "FileType + Version",
            8 => "StructOffset",
            12 => "StructCount",
            16 => "FieldOffset",
            20 => "FieldCount",
            24 => "LabelOffset",
            28 => "LabelCount",
            32 => "FieldDataOffset",
            36 => "FieldDataCount",
            40 => "FieldIndicesOffset",
            44 => "FieldIndicesCount",
            48 => "ListIndicesOffset",
            52 => "ListIndicesCount",
            _ => $"Unknown_{offset}"
        };
    }

    static void AnalyzePadding(string label, byte[] data)
    {
        int nullCount = 0;
        int totalPadding = 0;

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == 0x00)
            {
                nullCount++;
            }
            else if (nullCount > 3) // Found padding block
            {
                totalPadding += nullCount;
                nullCount = 0;
            }
            else
            {
                nullCount = 0;
            }
        }

        double paddingPercent = ((double)totalPadding / data.Length) * 100;
        Console.WriteLine($"{label}: {totalPadding:N0} padding bytes ({paddingPercent:F1}%)");
    }
}
