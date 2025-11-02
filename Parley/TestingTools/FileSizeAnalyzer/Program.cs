using System;
using System.IO;

void AnalyzeGffFile(string label, string path)
{
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

    Console.WriteLine($"\n{label}:");
    Console.WriteLine($"  File Size: {fs.Length} bytes");
    Console.WriteLine($"  Structs:       {structCount,4} structs × 12 bytes = {structCount * 12,5} bytes (offset: {structOffset})");
    Console.WriteLine($"  Fields:        {fieldCount,4} fields  × 12 bytes = {fieldCount * 12,5} bytes (offset: {fieldOffset})");
    Console.WriteLine($"  Labels:        {labelCount,4} labels  × 16 bytes = {labelCount * 16,5} bytes (offset: {labelOffset})");
    Console.WriteLine($"  Field Data:    {fieldDataCount,5} bytes (offset: {fieldDataOffset})");
    Console.WriteLine($"  Field Indices: {fieldIndicesCount,5} bytes (offset: {fieldIndicesOffset})");
    Console.WriteLine($"  List Indices:  {listIndicesCount,5} bytes (offset: {listIndicesOffset})");

    uint totalSections = 56 + (structCount * 12) + (fieldCount * 12) + (labelCount * 16) + fieldDataCount + fieldIndicesCount + listIndicesCount;
    Console.WriteLine($"  Total (calculated): {totalSections} bytes");
}

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║           FILE SIZE BREAKDOWN ANALYSIS                   ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

string origPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";
string exportPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_conditionalquest_test.dlg";

AnalyzeGffFile("ORIGINAL (Aurora)", origPath);
AnalyzeGffFile("EXPORTED (Conditional Quest Fix)", exportPath);

Console.WriteLine($"\n╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine($"║                    SIZE DIFFERENCES                        ║");
Console.WriteLine($"╚═══════════════════════════════════════════════════════════╝");

using var orig = new FileStream(origPath, FileMode.Open);
using var exp = new FileStream(exportPath, FileMode.Open);
using var origReader = new BinaryReader(orig);
using var expReader = new BinaryReader(exp);

// Skip to counts
origReader.ReadBytes(16);
expReader.ReadBytes(16);

uint origStructCount = origReader.ReadUInt32();
uint expStructCount = expReader.ReadUInt32();
origReader.ReadUInt32(); expReader.ReadUInt32();
uint origFieldCount = origReader.ReadUInt32();
uint expFieldCount = expReader.ReadUInt32();
origReader.ReadUInt32(); expReader.ReadUInt32();
uint origLabelCount = origReader.ReadUInt32();
uint expLabelCount = expReader.ReadUInt32();
origReader.ReadUInt32(); expReader.ReadUInt32();
uint origFieldDataCount = origReader.ReadUInt32();
uint expFieldDataCount = expReader.ReadUInt32();
origReader.ReadUInt32(); expReader.ReadUInt32();
uint origFieldIndicesCount = origReader.ReadUInt32();
uint expFieldIndicesCount = expReader.ReadUInt32();
origReader.ReadUInt32(); expReader.ReadUInt32();
uint origListIndicesCount = origReader.ReadUInt32();
uint expListIndicesCount = expReader.ReadUInt32();

Console.WriteLine($"\nStructs:       {origStructCount,4} → {expStructCount,4} (Δ {(int)expStructCount - (int)origStructCount,4}) = {((int)expStructCount - (int)origStructCount) * 12,5} bytes");
Console.WriteLine($"Fields:        {origFieldCount,4} → {expFieldCount,4} (Δ {(int)expFieldCount - (int)origFieldCount,4}) = {((int)expFieldCount - (int)origFieldCount) * 12,5} bytes");
Console.WriteLine($"Labels:        {origLabelCount,4} → {expLabelCount,4} (Δ {(int)expLabelCount - (int)origLabelCount,4}) = {((int)expLabelCount - (int)origLabelCount) * 16,5} bytes");
Console.WriteLine($"Field Data:    {origFieldDataCount,5} → {expFieldDataCount,5} (Δ {(int)expFieldDataCount - (int)origFieldDataCount,5}) bytes");
Console.WriteLine($"Field Indices: {origFieldIndicesCount,5} → {expFieldIndicesCount,5} (Δ {(int)expFieldIndicesCount - (int)origFieldIndicesCount,5}) bytes");
Console.WriteLine($"List Indices:  {origListIndicesCount,5} → {expListIndicesCount,5} (Δ {(int)expListIndicesCount - (int)origListIndicesCount,5}) bytes");

int totalDiff = ((int)expStructCount - (int)origStructCount) * 12 +
                ((int)expFieldCount - (int)origFieldCount) * 12 +
                ((int)expLabelCount - (int)origLabelCount) * 16 +
                ((int)expFieldDataCount - (int)origFieldDataCount) +
                ((int)expFieldIndicesCount - (int)origFieldIndicesCount) +
                ((int)expListIndicesCount - (int)origListIndicesCount);

Console.WriteLine($"\n══════════════════════════════════════════════════════════");
Console.WriteLine($"Total Difference: {totalDiff} bytes");
Console.WriteLine($"══════════════════════════════════════════════════════════");
