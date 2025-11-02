using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

class DialogComparison
{
    static void Main(string[] args)
    {
        var baseDir = @"~\Documents\Neverwinter Nights\modules\LNS_DLG";
        var files = new Dictionary<string, string>
        {
            ["chef_aurora"] = Path.Combine(baseDir, "chef.dlg"),
            ["chef_export"] = Path.Combine(baseDir, "chef01.dlg"),
            ["lista_aurora"] = Path.Combine(baseDir, "lista_new.dlg"),
            ["lista_export"] = Path.Combine(baseDir, "lista01.dlg"),
            ["myra_aurora"] = Path.Combine(baseDir, "myra_james.dlg"),
            ["myra_export"] = Path.Combine(baseDir, "myra01.dlg")
        };

        Console.WriteLine("=== DIALOG FILE COMPARISON ===\n");
        Console.WriteLine("File                  | Size    | Structs | Fields | Labels | FieldData | ListIndices | Struct Types");
        Console.WriteLine("----------------------|---------|---------|--------|--------|-----------|-------------|-------------");

        foreach (var kvp in files)
        {
            if (!File.Exists(kvp.Value))
            {
                Console.WriteLine($"{kvp.Key,-20} | FILE NOT FOUND");
                continue;
            }

            var info = AnalyzeGffFile(kvp.Value);
            Console.WriteLine($"{kvp.Key,-20} | {info.FileSize,7} | {info.StructCount,7} | {info.FieldCount,6} | {info.LabelCount,6} | {info.FieldDataSize,9} | {info.ListIndicesSize,11} | {string.Join(",", info.UniqueStructTypes.OrderBy(x => x).Take(5))}");
        }

        Console.WriteLine("\n=== STRUCT TYPE FREQUENCY ===");
        foreach (var kvp in files)
        {
            if (!File.Exists(kvp.Value)) continue;

            var info = AnalyzeGffFile(kvp.Value);
            Console.WriteLine($"\n{kvp.Key}:");
            foreach (var typeCount in info.StructTypeFrequency.OrderByDescending(x => x.Value).Take(10))
            {
                Console.WriteLine($"  Type {typeCount.Key,5}: {typeCount.Value,3} structs");
            }
        }

        Console.WriteLine("\n=== FIELD COUNT PER STRUCT ===");
        foreach (var kvp in files)
        {
            if (!File.Exists(kvp.Value)) continue;

            var info = AnalyzeGffFile(kvp.Value);
            Console.WriteLine($"\n{kvp.Key}: Avg={info.AverageFieldsPerStruct:F1}, Min={info.MinFieldsPerStruct}, Max={info.MaxFieldsPerStruct}");
        }

        Console.WriteLine("\n=== SECTION SIZE BREAKDOWN ===");
        Console.WriteLine("File                  | Structs | Fields  | Labels | FldIdx | FldData | LstIdx | Total");
        Console.WriteLine("----------------------|---------|---------|--------|--------|---------|--------|-------");
        foreach (var kvp in files)
        {
            if (!File.Exists(kvp.Value)) continue;

            var info = AnalyzeGffFile(kvp.Value);
            Console.WriteLine($"{kvp.Key,-20} | {info.StructSectionSize,7} | {info.FieldSectionSize,7} | {info.LabelSectionSize,6} | {info.FieldIndicesSize,6} | {info.FieldDataSize,7} | {info.ListIndicesSize,6} | {info.FileSize,6}");
        }
    }

    static GffInfo AnalyzeGffFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var info = new GffInfo { FileSize = bytes.Length };

        // Read header
        int structOffset = BitConverter.ToInt32(bytes, 0x08);
        int structCount = BitConverter.ToInt32(bytes, 0x0C);
        int fieldOffset = BitConverter.ToInt32(bytes, 0x10);
        int fieldCount = BitConverter.ToInt32(bytes, 0x14);
        int labelOffset = BitConverter.ToInt32(bytes, 0x18);
        int labelCount = BitConverter.ToInt32(bytes, 0x1C);
        int fieldDataOffset = BitConverter.ToInt32(bytes, 0x20);
        int fieldDataCount = BitConverter.ToInt32(bytes, 0x24);
        int fieldIndicesOffset = BitConverter.ToInt32(bytes, 0x28);
        int fieldIndicesCount = BitConverter.ToInt32(bytes, 0x2C);
        int listIndicesOffset = BitConverter.ToInt32(bytes, 0x30);
        int listIndicesCount = BitConverter.ToInt32(bytes, 0x34);

        info.StructCount = structCount;
        info.FieldCount = fieldCount;
        info.LabelCount = labelCount;
        info.FieldDataSize = fieldDataCount;
        info.ListIndicesSize = listIndicesCount;
        info.FieldIndicesSize = fieldIndicesCount;
        info.StructSectionSize = structCount * 12; // Each struct = 12 bytes
        info.FieldSectionSize = fieldCount * 12; // Each field = 12 bytes
        info.LabelSectionSize = labelCount * 16; // Each label = 16 bytes

        // Read struct types and field counts
        for (int i = 0; i < structCount; i++)
        {
            int pos = structOffset + (i * 12);
            uint type = BitConverter.ToUInt32(bytes, pos);
            uint fieldCountForStruct = BitConverter.ToUInt32(bytes, pos + 8);

            info.UniqueStructTypes.Add(type);
            if (!info.StructTypeFrequency.ContainsKey(type))
                info.StructTypeFrequency[type] = 0;
            info.StructTypeFrequency[type]++;

            info.FieldCountsPerStruct.Add(fieldCountForStruct);
        }

        if (info.FieldCountsPerStruct.Count > 0)
        {
            info.AverageFieldsPerStruct = info.FieldCountsPerStruct.Select(x => (double)x).Average();
            info.MinFieldsPerStruct = info.FieldCountsPerStruct.Min();
            info.MaxFieldsPerStruct = info.FieldCountsPerStruct.Max();
        }

        return info;
    }
}

class GffInfo
{
    public int FileSize;
    public int StructCount;
    public int FieldCount;
    public int LabelCount;
    public int FieldDataSize;
    public int ListIndicesSize;
    public int FieldIndicesSize;
    public int StructSectionSize;
    public int FieldSectionSize;
    public int LabelSectionSize;
    public HashSet<uint> UniqueStructTypes = new HashSet<uint>();
    public Dictionary<uint, int> StructTypeFrequency = new Dictionary<uint, int>();
    public List<uint> FieldCountsPerStruct = new List<uint>();
    public double AverageFieldsPerStruct;
    public uint MinFieldsPerStruct;
    public uint MaxFieldsPerStruct;
}
