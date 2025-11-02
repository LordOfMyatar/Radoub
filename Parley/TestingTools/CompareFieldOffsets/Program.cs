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

        Console.WriteLine("=== FIELD OFFSET COMPARISON ===\n");

        var origFields = ReadFields(originalFile);
        var expFields = ReadFields(exportedFile);

        Console.WriteLine($"Original: {origFields.Count} fields");
        Console.WriteLine($"Exported: {expFields.Count} fields\n");

        // Find fields with offset=0 in exported but offset>0 in original
        var differences = new List<string>();

        for (int i = 0; i < Math.Min(origFields.Count, expFields.Count); i++)
        {
            var orig = origFields[i];
            var exp = expFields[i];

            // Check if types match
            if (orig.type != exp.type)
            {
                differences.Add($"Field {i}: TYPE MISMATCH (orig={orig.type}, exp={exp.type})");
                continue;
            }

            // For string types (CExoString=10, CResRef=8, CExoLocString=12)
            if (orig.type == 8 || orig.type == 10 || orig.type == 12)
            {
                // Check if exported has offset=0 but original has offset>0
                if (exp.offset == 0 && orig.offset > 0)
                {
                    string typeName = orig.type == 8 ? "CResRef" : orig.type == 10 ? "CExoString" : "CExoLocString";
                    differences.Add($"Field {i}: {typeName} - Exported offset=0, Original offset={orig.offset}");
                }
                // Also check if both have offsets but they differ significantly
                else if (exp.offset > 0 && orig.offset > 0 && Math.Abs((int)exp.offset - (int)orig.offset) > 100)
                {
                    string typeName = orig.type == 8 ? "CResRef" : orig.type == 10 ? "CExoString" : "CExoLocString";
                    differences.Add($"Field {i}: {typeName} - Offset differs: orig={orig.offset}, exp={exp.offset}");
                }
            }
        }

        if (differences.Any())
        {
            Console.WriteLine($"Found {differences.Count} field offset differences:\n");
            foreach (var diff in differences.Take(50))
            {
                Console.WriteLine($"  {diff}");
            }

            if (differences.Count > 50)
            {
                Console.WriteLine($"\n  ... and {differences.Count - 50} more");
            }
        }
        else
        {
            Console.WriteLine("✅ No significant field offset differences found!");
        }

        // Count how many of each type have offset=0
        Console.WriteLine("\n=== FIELDS WITH OFFSET=0 ===");
        CountZeroOffsets(origFields, "Original");
        CountZeroOffsets(expFields, "Exported");
    }

    static List<(uint type, uint offset)> ReadFields(string filePath)
    {
        var fields = new List<(uint type, uint offset)>();

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        br.BaseStream.Seek(8, SeekOrigin.Begin);
        br.ReadUInt32(); br.ReadUInt32(); // struct
        uint fieldOffset = br.ReadUInt32();
        uint fieldCount = br.ReadUInt32();

        br.BaseStream.Seek(fieldOffset, SeekOrigin.Begin);

        for (uint i = 0; i < fieldCount; i++)
        {
            uint fieldType = br.ReadUInt32();
            uint labelIndex = br.ReadUInt32();
            uint dataOrOffset = br.ReadUInt32();

            fields.Add((fieldType, dataOrOffset));
        }

        return fields;
    }

    static void CountZeroOffsets(List<(uint type, uint offset)> fields, string label)
    {
        var byType = new Dictionary<uint, (int total, int zero)>();

        foreach (var (type, offset) in fields)
        {
            // Only count string types
            if (type == 8 || type == 10 || type == 12)
            {
                if (!byType.ContainsKey(type))
                    byType[type] = (0, 0);

                var current = byType[type];
                byType[type] = (current.total + 1, current.zero + (offset == 0 ? 1 : 0));
            }
        }

        Console.WriteLine($"\n{label}:");
        foreach (var kvp in byType.OrderBy(x => x.Key))
        {
            string typeName = kvp.Key == 8 ? "CResRef" : kvp.Key == 10 ? "CExoString" : "CExoLocString";
            var (total, zero) = kvp.Value;
            Console.WriteLine($"  {typeName}: {zero}/{total} have offset=0 ({total - zero} point to FieldData)");
        }
    }
}
