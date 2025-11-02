using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string originalFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";
        string exportedFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\__chef.dlg";

        Console.WriteLine("=== FIELDDATA STRING SEARCH ===\n");

        var origFieldData = ExtractFieldData(originalFile);
        var expFieldData = ExtractFieldData(exportedFile);

        // Extract all CExoString values from original
        var origStrings = ExtractCExoStrings(origFieldData);
        var expStrings = ExtractCExoStrings(expFieldData);

        Console.WriteLine($"Original has {origStrings.Count} strings, total {origFieldData.Length} bytes");
        Console.WriteLine($"Exported has {expStrings.Count} strings, total {expFieldData.Length} bytes\n");

        // Find strings in original but not in exported
        var missing = new List<string>();
        foreach (var str in origStrings)
        {
            if (!expStrings.Contains(str))
            {
                missing.Add(str);
            }
        }

        if (missing.Count > 0)
        {
            Console.WriteLine($"Found {missing.Count} strings in original NOT in exported:");
            foreach (var str in missing)
            {
                Console.WriteLine($"  - '{str}' ({str.Length} chars)");
            }

            // Calculate total missing bytes (length prefix + string + padding)
            int totalMissing = 0;
            foreach (var str in missing)
            {
                int strBytes = Encoding.UTF8.GetByteCount(str);
                int withPrefix = 4 + strBytes; // 4-byte length prefix + string
                int withPadding = ((withPrefix + 3) / 4) * 4; // Round up to 4-byte boundary
                totalMissing += withPadding;
            }
            Console.WriteLine($"\nEstimated missing bytes: {totalMissing}");
        }
        else
        {
            Console.WriteLine("✅ All strings from original are present in exported!");
        }
    }

    static byte[] ExtractFieldData(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        br.BaseStream.Seek(8, SeekOrigin.Begin);
        br.ReadUInt32(); br.ReadUInt32(); // struct
        br.ReadUInt32(); br.ReadUInt32(); // field
        br.ReadUInt32(); br.ReadUInt32(); // label

        uint fieldDataOffset = br.ReadUInt32();
        uint fieldDataCount = br.ReadUInt32();

        br.BaseStream.Seek(fieldDataOffset, SeekOrigin.Begin);
        return br.ReadBytes((int)fieldDataCount);
    }

    static List<string> ExtractCExoStrings(byte[] fieldData)
    {
        var strings = new List<string>();
        int pos = 0;

        while (pos < fieldData.Length - 4)
        {
            // Read potential length prefix
            uint length = BitConverter.ToUInt32(fieldData, pos);

            // Sanity check: length should be reasonable (< 10000) and fit in remaining data
            if (length > 0 && length < 10000 && pos + 4 + length <= fieldData.Length)
            {
                try
                {
                    string str = Encoding.UTF8.GetString(fieldData, pos + 4, (int)length);
                    // Check if it's a valid string (printable ASCII or reasonable UTF-8)
                    if (IsReasonableString(str))
                    {
                        strings.Add(str);
                        pos += 4 + (int)length;
                        continue;
                    }
                }
                catch { }
            }

            pos++;
        }

        return strings;
    }

    static bool IsReasonableString(string str)
    {
        if (string.IsNullOrEmpty(str)) return false;

        // Must be mostly printable or whitespace
        int printable = 0;
        foreach (char c in str)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c) || c == '_' || c == '-')
                printable++;
        }

        return printable >= str.Length * 0.8; // At least 80% reasonable characters
    }
}
