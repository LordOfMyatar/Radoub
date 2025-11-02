using System;
using System.IO;

string path = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";

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

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║           ROOT STRUCT ANALYSIS (lista_orig.dlg)          ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

// Read root struct (Struct[0])
br.BaseStream.Seek(structOffset, SeekOrigin.Begin);

uint rootType = br.ReadUInt32();
uint rootDataOrFileIndex = br.ReadUInt32();
uint rootFieldCount = br.ReadUInt32();

Console.WriteLine($"Root Struct (Struct[0]):");
Console.WriteLine($"  Type: {(rootType == 0xFFFFFFFF ? "Root (0xFFFFFFFF)" : rootType.ToString())}");
Console.WriteLine($"  FirstFieldIdx: {rootDataOrFileIndex}");
Console.WriteLine($"  FieldCount: {rootFieldCount}\n");

// Read root struct fields
br.BaseStream.Seek(fieldOffset + (rootDataOrFileIndex * 12), SeekOrigin.Begin);

Console.WriteLine($"{"Field#",-8} {"Type",-15} {"LabelIdx",-10} {"Label",-20} {"Data/Offset",-12}");
Console.WriteLine(new string('-', 75));

for (int i = 0; i < rootFieldCount; i++)
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
        4 => "DWORD",
        10 => "CExoString",
        11 => "CResRef",
        12 => "CExoLocString",
        15 => "List",
        _ => $"Type-{fieldType}"
    };

    Console.WriteLine($"{i,-8} {fieldTypeStr,-15} {labelIdx,-10} {fieldLabel,-20} {fieldData,-12}");

    // If this is a List field, read the list contents
    if (fieldType == 15) // List
    {
        long currentPos = br.BaseStream.Position;

        // Lists are stored in the list indices section
        // The fieldData contains an offset into the list indices section
        br.BaseStream.Seek(listIndicesOffset + fieldData, SeekOrigin.Begin);

        uint listCount = br.ReadUInt32();
        Console.WriteLine($"         └─> List contains {listCount} structs:");

        for (int j = 0; j < listCount && j < 10; j++) // Limit to first 10 entries
        {
            uint structIndex = br.ReadUInt32();
            Console.WriteLine($"             [{j}] → Struct[{structIndex}]");
        }

        if (listCount > 10)
            Console.WriteLine($"             ... and {listCount - 10} more");

        br.BaseStream.Seek(currentPos, SeekOrigin.Begin);
    }
}

Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║           ALL STRUCTS OVERVIEW                            ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

br.BaseStream.Seek(structOffset, SeekOrigin.Begin);

Console.WriteLine($"{"Index",-6} {"Type",-12} {"FieldCount",-12}");
Console.WriteLine(new string('-', 30));

for (int i = 0; i < structCount; i++)
{
    uint type = br.ReadUInt32();
    br.ReadUInt32(); // dataOrFileIndex
    uint fieldCnt = br.ReadUInt32();

    string typeStr = type == 0xFFFFFFFF ? "Root" : $"Type-{type}";
    Console.WriteLine($"{i,-6} {typeStr,-12} {fieldCnt,-12}");
}
