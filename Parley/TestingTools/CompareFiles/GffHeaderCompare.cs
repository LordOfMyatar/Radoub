using System;
using System.IO;

void CompareHeaders(string path)
{
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
    Console.WriteLine($"\n{fileName}:");
    Console.WriteLine($"  Structs: {structCount} @ {structOffset}");
    Console.WriteLine($"  Fields: {fieldCount} @ {fieldOffset}");
    Console.WriteLine($"  Labels: {labelCount} @ {labelOffset}");
    Console.WriteLine($"  FieldData: {fieldDataCount} bytes @ {fieldDataOffset}");
    Console.WriteLine($"  FieldIndices: {fieldIndicesCount} @ {fieldIndicesOffset}");
    Console.WriteLine($"  ListIndices: {listIndicesCount} @ {listIndicesOffset}");
    Console.WriteLine($"  Total file size: {new FileInfo(path).Length} bytes");
}

Console.WriteLine("=== GFF HEADER COMPARISON ===");
CompareHeaders(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg");
CompareHeaders(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista01.dlg");
CompareHeaders(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_research.dlg");
