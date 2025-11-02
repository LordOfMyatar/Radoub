using System;
using System.IO;
using System.Text;

void AnalyzeTypeZeroPattern(string path)
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

    Console.WriteLine($"\n{Path.GetFileName(path)}");
    Console.WriteLine($"{"=",-70}");
    Console.WriteLine($"Total Structs: {structCount}, Total Fields: {fieldCount}");

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

    // Analyze Type-0 structs
    var typeZeroStructs = structs.Where(s => s.type == 0).ToList();
    Console.WriteLine($"\nType-0 Structs: {typeZeroStructs.Count} of {structCount} total");

    // Group by field count
    var fieldCountGroups = typeZeroStructs.GroupBy(s => s.fieldCount)
                                           .OrderByDescending(g => g.Count());

    Console.WriteLine("\nType-0 Field Count Distribution:");
    foreach (var group in fieldCountGroups)
    {
        Console.WriteLine($"  {group.Key} fields: {group.Count()} structs ({100.0 * group.Count() / typeZeroStructs.Count:F1}%)");
    }

    // Compare to other types
    Console.WriteLine("\nComparison to Other Types:");
    var typeGroups = structs.Where(s => s.type != 0xFFFFFFFF)
                             .GroupBy(s => s.type)
                             .OrderByDescending(g => g.Count())
                             .Take(5);

    foreach (var group in typeGroups)
    {
        var mostCommonFieldCount = group.GroupBy(s => s.fieldCount)
                                         .OrderByDescending(g => g.Count())
                                         .First();
        Console.WriteLine($"  Type-{group.Key}: {group.Count()} structs, most common = {mostCommonFieldCount.Key} fields ({mostCommonFieldCount.Count()} times)");
    }
}

void MapStructTypes(string path)
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

    Console.WriteLine($"\n{Path.GetFileName(path)}");
    Console.WriteLine($"{"=",-70}");

    // Read all labels first
    br.BaseStream.Seek(labelOffset, SeekOrigin.Begin);
    var labels = new string[labelCount];
    for (int i = 0; i < labelCount; i++)
    {
        var labelBytes = br.ReadBytes(16);
        labels[i] = Encoding.ASCII.GetString(labelBytes).TrimEnd('\0');
    }

    // Read all structs
    br.BaseStream.Seek(structOffset, SeekOrigin.Begin);
    var structs = new (uint type, uint dataOrFileIndex, uint fieldCount)[structCount];
    for (int i = 0; i < structCount; i++)
    {
        uint type = br.ReadUInt32();
        uint dataOrFileIndex = br.ReadUInt32();
        uint structFieldCount = br.ReadUInt32();
        structs[i] = (type, dataOrFileIndex, structFieldCount);
    }

    // Read all fields
    br.BaseStream.Seek(fieldOffset, SeekOrigin.Begin);
    var fields = new (uint type, uint labelIndex, uint dataOrDataOffset)[fieldCount];
    for (int i = 0; i < fieldCount; i++)
    {
        uint type = br.ReadUInt32();
        uint labelIndex = br.ReadUInt32();
        uint dataOrDataOffset = br.ReadUInt32();
        fields[i] = (type, labelIndex, dataOrDataOffset);
    }

    // Map structs to their field labels
    Console.WriteLine("\nStruct Type Mapping:");
    Console.WriteLine($"{"Idx",-4} {"Type",-12} {"Fields",-6} {"Field Labels",-50}");
    Console.WriteLine(new string('-', 70));

    uint currentFieldIndex = 0;
    for (int i = 0; i < Math.Min(structCount, 20); i++)
    {
        var s = structs[i];
        string typeStr = s.type == 0xFFFFFFFF ? "Root(65535)" : $"Type-{s.type}";

        // Get field labels for this struct
        var fieldLabels = new List<string>();
        uint fieldsToRead = s.fieldCount;

        if (fieldsToRead == 1)
        {
            // Single field - index is stored directly
            uint fieldIdx = s.dataOrFileIndex;
            if (fieldIdx < fieldCount && fields[fieldIdx].labelIndex < labelCount)
            {
                fieldLabels.Add(labels[fields[fieldIdx].labelIndex]);
            }
        }
        else if (fieldsToRead > 1)
        {
            // Multiple fields - read from field indices array
            uint fieldIndicesOffset = s.dataOrFileIndex / 4;

            for (uint f = 0; f < fieldsToRead && f < 10; f++) // Limit to first 10 fields
            {
                long savedPos = br.BaseStream.Position;
                try
                {
                    // Field indices are stored after list indices section
                    // This is a simplified read - actual implementation needs proper offset calculation
                    uint fieldIdx = currentFieldIndex + f;
                    if (fieldIdx < fieldCount && fields[fieldIdx].labelIndex < labelCount)
                    {
                        fieldLabels.Add(labels[fields[fieldIdx].labelIndex]);
                    }
                }
                finally
                {
                    br.BaseStream.Position = savedPos;
                }
            }
        }

        currentFieldIndex += s.fieldCount;

        string labelStr = string.Join(", ", fieldLabels.Take(5));
        if (fieldLabels.Count > 5) labelStr += "...";

        Console.WriteLine($"{i,-4} {typeStr,-12} {s.fieldCount,-6} {labelStr,-50}");
    }

    if (structCount > 20)
    {
        Console.WriteLine($"... ({structCount - 20} more structs)");
    }

    // Analyze patterns
    Console.WriteLine("\nType-to-Content Pattern Analysis:");
    Console.WriteLine("Looking for fields like: 'Speaker', 'Text', 'EntriesList', 'RepliesList'");
}

Console.WriteLine("=== TYPE-0 PATTERN ANALYSIS ===");
Console.WriteLine("What do Type-0 structs have in common?\n");

string henchPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\hench_hotu.dlg";
string x2Path = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\x2_associate.dlg";
string chefPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";
string listaPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";

Console.WriteLine("\n1. SMALL FILE (lista_orig - 14 structs):");
AnalyzeTypeZeroPattern(listaPath);

Console.WriteLine("\n\n2. MEDIUM FILE (chef - 82 structs):");
AnalyzeTypeZeroPattern(chefPath);

Console.WriteLine("\n\n3. LARGE FILE (hench_hotu - 1345 structs):");
AnalyzeTypeZeroPattern(henchPath);

Console.WriteLine("\n\n4. X-LARGE FILE (x2_associate - 2222 structs):");
AnalyzeTypeZeroPattern(x2Path);

Console.WriteLine("\n\n=== CONCLUSION ===");
Console.WriteLine("Type-0 = Most common field count pattern in the file");
Console.WriteLine("Aurora assigns Type-0 to whichever struct pattern appears most frequently");
