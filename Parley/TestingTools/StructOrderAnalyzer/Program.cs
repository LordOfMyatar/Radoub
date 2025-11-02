using System;
using System.IO;

void ShowStructOrder(string label, string path)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"{label}: FILE NOT FOUND");
        return;
    }

    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
    using var br = new BinaryReader(fs);

    br.ReadChars(4); // FileType
    br.ReadChars(4); // FileVersion

    uint structOffset = br.ReadUInt32();
    uint structCount = br.ReadUInt32();

    br.BaseStream.Seek(structOffset, SeekOrigin.Begin);

    Console.WriteLine($"\n{label}:");
    Console.WriteLine($"{"Index",-6} {"Type",-12} {"FieldCount",-12}");
    Console.WriteLine(new string('-', 30));

    for (int i = 0; i < structCount; i++)
    {
        uint type = br.ReadUInt32();
        br.ReadUInt32(); // dataOrFileIndex
        uint fieldCount = br.ReadUInt32();

        string typeStr = type == 0xFFFFFFFF ? "Root" : $"Type-{type}";
        Console.WriteLine($"{i,-6} {typeStr,-12} {fieldCount,-12}");
    }
}

Console.WriteLine("=== STRUCT ORDER COMPARISON ===");

string origPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";
string exportPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_conditionalquest_test.dlg";

ShowStructOrder("ORIGINAL", origPath);
ShowStructOrder("EXPORTED", exportPath);
