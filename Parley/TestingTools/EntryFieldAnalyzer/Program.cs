using System;
using System.IO;

void AnalyzeEntryFields(string label, string path)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"{label}: FILE NOT FOUND - {path}");
        return;
    }

    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
    using var br = new BinaryReader(fs);

    // Read GFF header
    br.ReadChars(4); // FileType
    br.ReadChars(4); // FileVersion

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

    Console.WriteLine($"Header Info:");
    Console.WriteLine($"  Struct Offset: {structOffset}, Count: {structCount}");
    Console.WriteLine($"  Field Offset: {fieldOffset}, Count: {fieldCount}");
    Console.WriteLine($"  Label Offset: {labelOffset}, Count: {labelCount}");
    Console.WriteLine($"  FieldData Offset: {fieldDataOffset}, Count: {fieldDataCount}");
    Console.WriteLine($"  FieldIndices Offset: {fieldIndicesOffset}, Count: {fieldIndicesCount}");
    Console.WriteLine($"  ListIndices Offset: {listIndicesOffset}, Count: {listIndicesCount}");

    // Read all structs to find entry structs
    br.BaseStream.Seek(structOffset, SeekOrigin.Begin);

    Console.WriteLine($"\n{label}:");
    Console.WriteLine($"{"Index",-6} {"Type",-12} {"FieldCount",-12} {"FirstFieldIdx",-15}");
    Console.WriteLine(new string('-', 45));

    var entryStructs = new List<(int index, uint type, uint fieldCount, uint firstFieldIdx)>();

    for (int i = 0; i < structCount; i++)
    {
        uint type = br.ReadUInt32();
        uint dataOrFileIndex = br.ReadUInt32();
        uint fieldCnt = br.ReadUInt32();

        string typeStr = type == 0xFFFFFFFF ? "Root" : $"Type-{type}";
        Console.WriteLine($"{i,-6} {typeStr,-12} {fieldCnt,-12} {dataOrFileIndex,-15}");

        // Entry structs typically have 10-12 fields (not 4 like pointers)
        if (type != 0xFFFFFFFF && fieldCnt >= 10)
        {
            entryStructs.Add((i, type, fieldCnt, dataOrFileIndex));
        }
    }

    // Analyze ALL structs with 10+ fields
    if (entryStructs.Count > 0)
    {
        foreach (var entry in entryStructs)
        {
            Console.WriteLine($"\nStruct[{entry.index}]:");
            Console.WriteLine($"  Type: {entry.type}, Fields: {entry.fieldCount}, FirstFieldIdx: {entry.firstFieldIdx}");

            br.BaseStream.Seek(fieldOffset + (entry.firstFieldIdx * 12), SeekOrigin.Begin);

            Console.WriteLine($"\n  {"Field#",-8} {"Type",-10} {"LabelIdx",-10} Label");
            Console.WriteLine($"  {new string('-', 60)}");

            for (int i = 0; i < entry.fieldCount; i++)
            {
                uint fieldType = br.ReadUInt32();
                uint labelIdx = br.ReadUInt32();
                uint fieldData = br.ReadUInt32();

                // Read label
                long savedPos = br.BaseStream.Position;
                br.BaseStream.Seek(labelOffset + (labelIdx * 16), SeekOrigin.Begin);
                string fieldLabel = new string(br.ReadChars(16)).TrimEnd('\0');
                br.BaseStream.Seek(savedPos, SeekOrigin.Begin);

                string fieldTypeStr = fieldType switch
                {
                    0 => "BYTE",
                    1 => "CHAR",
                    2 => "WORD",
                    3 => "SHORT",
                    4 => "DWORD",
                    5 => "INT",
                    6 => "DWORD64",
                    7 => "INT64",
                    8 => "FLOAT",
                    9 => "DOUBLE",
                    10 => "CExoString",
                    11 => "CResRef",
                    12 => "CExoLocString",
                    13 => "VOID",
                    14 => "Struct",
                    15 => "List",
                    _ => $"Unknown({fieldType})"
                };

                Console.WriteLine($"  {i,-8} {fieldTypeStr,-10} {labelIdx,-10} {fieldLabel}");
            }
        }
    }
}

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║           ENTRY FIELD COUNT ANALYSIS                      ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

string origPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";
string exportPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_phase1_export.dlg";

AnalyzeEntryFields("ORIGINAL (Original format)", origPath);
AnalyzeEntryFields("EXPORTED (Phase 1 fix)", exportPath);

Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    FIELD COUNT COMPARISON                  ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

Console.WriteLine("\nIf Original = 11 and Exported = 12, we're adding an extra field.");
Console.WriteLine("Check which field appears in Exported but not in Original.");
