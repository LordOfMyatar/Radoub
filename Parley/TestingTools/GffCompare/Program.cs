using System;
using System.IO;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== GFF HEADER COMPARISON ===\n");

        CompareHeaders(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg");
        CompareHeaders(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\__chef.dlg");
    }

    static void CompareHeaders(string path)
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
        var fileSize = new FileInfo(path).Length;

        Console.WriteLine($"\n{fileName}:");
        Console.WriteLine($"  File Size: {fileSize} bytes");
        Console.WriteLine($"  Type: {fileType}, Version: {fileVersion}");
        Console.WriteLine($"  Structs: {structCount} @ offset {structOffset}");
        Console.WriteLine($"  Fields: {fieldCount} @ offset {fieldOffset}");
        Console.WriteLine($"  Labels: {labelCount} @ offset {labelOffset}");
        Console.WriteLine($"  FieldData: {fieldDataCount} bytes @ offset {fieldDataOffset}");
        Console.WriteLine($"  FieldIndices: {fieldIndicesCount} @ offset {fieldIndicesOffset}");
        Console.WriteLine($"  ListIndices: {listIndicesCount} @ offset {listIndicesOffset}");

        // Calculate ratios
        if (structCount > 0)
        {
            double fieldsPerStruct = (double)fieldCount / structCount;
            Console.WriteLine($"  Ratio - Fields/Structs: {fieldsPerStruct:F2}");
        }

        if (fieldCount > 0)
        {
            double indicesPerField = (double)fieldIndicesCount / fieldCount;
            Console.WriteLine($"  Ratio - FieldIndices/Fields: {indicesPerField:F2}");
        }
    }
}
