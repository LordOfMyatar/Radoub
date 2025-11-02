using System;
using System.IO;
using System.Collections.Generic;

void AnalyzeStructLayout(string path)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"File not found: {path}");
        return;
    }

    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
    using var br = new BinaryReader(fs);

    // Read GFF header
    string fileType = new string(br.ReadChars(4));
    string fileVersion = new string(br.ReadChars(4));

    uint structOffset = br.ReadUInt32();
    uint structCount = br.ReadUInt32();
    uint fieldOffset = br.ReadUInt32();
    uint fieldCount = br.ReadUInt32();
    uint labelOffset = br.ReadUInt32();
    uint labelCount = br.ReadUInt32();
    uint fieldDataOffset = br.ReadUInt32();
    uint fieldDataCount = br.ReadUInt32();
    uint fieldIndicesOffset = br.ReadUInt32();
    uint fieldIndicesCount = br.ReadUInt32();
    uint listIndicesOffset = br.ReadUInt32();
    uint listIndicesCount = br.ReadUInt32();

    var fileName = Path.GetFileName(path);
    Console.WriteLine($"\n{'=',-60}");
    Console.WriteLine($"{fileName}");
    Console.WriteLine($"{'=',-60}");
    Console.WriteLine($"Structs: {structCount}, Fields: {fieldCount}, Labels: {labelCount}");

    // Read all structs
    br.BaseStream.Seek(structOffset, SeekOrigin.Begin);
    var structs = new List<(uint type, uint dataOrFileIndex, uint fieldCount)>();

    for (int i = 0; i < structCount; i++)
    {
        uint type = br.ReadUInt32();
        uint dataOrFileIndex = br.ReadUInt32();
        uint structFieldCount = br.ReadUInt32();
        structs.Add((type, dataOrFileIndex, structFieldCount));
    }

    // Analyze struct types and field distribution
    var typeFrequency = new Dictionary<uint, int>();
    var fieldDistribution = new Dictionary<uint, int>();

    foreach (var s in structs)
    {
        if (!typeFrequency.ContainsKey(s.type))
            typeFrequency[s.type] = 0;
        typeFrequency[s.type]++;

        if (!fieldDistribution.ContainsKey(s.fieldCount))
            fieldDistribution[s.fieldCount] = 0;
        fieldDistribution[s.fieldCount]++;
    }

    Console.WriteLine("\nStruct Type Distribution:");
    foreach (var kvp in typeFrequency)
    {
        string typeStr = kvp.Key == 0xFFFFFFFF ? "Root(65535)" : kvp.Key == 0 ? "Type-0" : $"Type-{kvp.Key}";
        Console.WriteLine($"  {typeStr}: {kvp.Value} structs");
    }

    Console.WriteLine("\nField Count Distribution:");
    foreach (var kvp in fieldDistribution.OrderBy(x => x.Key))
    {
        Console.WriteLine($"  {kvp.Key} fields: {kvp.Value} structs");
    }

    Console.WriteLine("\nDetailed Struct Breakdown:");
    for (int i = 0; i < Math.Min(structCount, 10); i++)
    {
        var s = structs[i];
        string typeStr = s.type == 0xFFFFFFFF ? "Root" : s.type == 0 ? "T0" : $"T{s.type}";
        Console.WriteLine($"  Struct[{i,2}]: Type={typeStr,-8} Fields={s.fieldCount,2} DataIndex={s.dataOrFileIndex}");
    }

    if (structCount > 10)
    {
        Console.WriteLine($"  ... ({structCount - 10} more structs)");
    }
}

Console.WriteLine("=== STRUCT LAYOUT ANALYSIS ===");
Console.WriteLine("Comparing struct decomposition patterns between files\n");

string blankPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\blank_file.dlg";
string listaPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";
string mediumPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";
string largePath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\hench_hotu.dlg";
string xlargestPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\x2_associate.dlg";

Console.WriteLine("\n1. BLANK FILE (Aurora's default for new files):");
AnalyzeStructLayout(blankPath);

Console.WriteLine("\n2. SMALL FILE (3 entries, 3 replies):");
AnalyzeStructLayout(listaPath);

Console.WriteLine("\n3. MEDIUM FILE (11KB):");
AnalyzeStructLayout(mediumPath);

Console.WriteLine("\n4. LARGE FILE (176KB - complex henchman):");
AnalyzeStructLayout(largePath);

Console.WriteLine("\n5. X-LARGE FILE (286KB - x2_associate BioWare official):");
AnalyzeStructLayout(xlargestPath);

Console.WriteLine("\n=== KEY INSIGHTS ===");
Console.WriteLine("Looking for:");
Console.WriteLine("  1. Type-0 vs other struct types usage");
Console.WriteLine("  2. Field count distribution patterns");
Console.WriteLine("  3. Struct hierarchy differences");
Console.WriteLine("  4. Which structs Aurora adds during correction");
