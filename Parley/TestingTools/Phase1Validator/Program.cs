using System;
using System.IO;

void CompareStructTypes(string label, string path)
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

    // Read all structs
    br.BaseStream.Seek(structOffset, SeekOrigin.Begin);
    var typeFrequency = new Dictionary<uint, int>();

    for (int i = 0; i < structCount; i++)
    {
        uint type = br.ReadUInt32();
        br.ReadUInt32(); // dataOrFileIndex
        br.ReadUInt32(); // fieldCount

        if (!typeFrequency.ContainsKey(type))
            typeFrequency[type] = 0;
        typeFrequency[type]++;
    }

    var fileSize = new FileInfo(path).Length;

    Console.WriteLine($"\n{label}:");
    Console.WriteLine($"  File: {Path.GetFileName(path)}");
    Console.WriteLine($"  Size: {fileSize} bytes");
    Console.WriteLine($"  Structs: {structCount}, Fields: {fieldCount}");
    Console.WriteLine($"  Type Distribution:");

    foreach (var kvp in typeFrequency.OrderBy(x => x.Key))
    {
        string typeStr = kvp.Key == 0xFFFFFFFF ? "Root(65535)" : $"Type-{kvp.Key}";
        Console.WriteLine($"    {typeStr}: {kvp.Value} structs");
    }
}

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║         PHASE 1 FIX VALIDATION: Struct Type Preservation  ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

string origPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";
string exportPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_phase1_export.dlg";

CompareStructTypes("ORIGINAL (Original format)", origPath);
CompareStructTypes("EXPORTED (Phase 1 fix)", exportPath);

Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    VALIDATION CHECKS                       ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

if (!File.Exists(exportPath))
{
    Console.WriteLine("\n❌ EXPORT FILE NOT FOUND!");
    Console.WriteLine("   Please:");
    Console.WriteLine("   1. Open ArcReactor");
    Console.WriteLine("   2. Load: lista_orig.dlg");
    Console.WriteLine("   3. Save As: lista_phase1_export.dlg");
    Console.WriteLine("   4. Re-run this validator");
    Environment.Exit(1);
}

using var origFs = new FileStream(origPath, FileMode.Open, FileAccess.Read);
using var exportFs = new FileStream(exportPath, FileMode.Open, FileAccess.Read);
using var origBr = new BinaryReader(origFs);
using var exportBr = new BinaryReader(exportFs);

// Compare headers
origBr.BaseStream.Seek(8, SeekOrigin.Begin);
exportBr.BaseStream.Seek(8, SeekOrigin.Begin);

uint origStructOffset = origBr.ReadUInt32();
uint origStructCount = origBr.ReadUInt32();
uint exportStructOffset = exportBr.ReadUInt32();
uint exportStructCount = exportBr.ReadUInt32();

Console.WriteLine($"\n✓ Struct Count Match: {origStructCount} vs {exportStructCount} {(origStructCount == exportStructCount ? "✅ PASS" : "❌ FAIL")}");

// Compare struct types
origBr.BaseStream.Seek(origStructOffset, SeekOrigin.Begin);
exportBr.BaseStream.Seek(exportStructOffset, SeekOrigin.Begin);

bool allTypesMatch = true;
var mismatches = new List<string>();

for (int i = 0; i < Math.Min(origStructCount, exportStructCount); i++)
{
    uint origType = origBr.ReadUInt32();
    origBr.ReadUInt32(); // skip
    origBr.ReadUInt32(); // skip

    uint exportType = exportBr.ReadUInt32();
    exportBr.ReadUInt32(); // skip
    exportBr.ReadUInt32(); // skip

    if (origType != exportType)
    {
        allTypesMatch = false;
        string origTypeStr = origType == 0xFFFFFFFF ? "Root" : $"Type-{origType}";
        string exportTypeStr = exportType == 0xFFFFFFFF ? "Root" : $"Type-{exportType}";
        mismatches.Add($"  Struct[{i}]: {origTypeStr} → {exportTypeStr}");
    }
}

Console.WriteLine($"✓ Struct Types Match: {(allTypesMatch ? "✅ PASS - All types preserved!" : "❌ FAIL")}");

if (!allTypesMatch)
{
    Console.WriteLine("\n  Mismatches:");
    foreach (var mismatch in mismatches.Take(10))
    {
        Console.WriteLine(mismatch);
    }
    if (mismatches.Count > 10)
        Console.WriteLine($"  ... and {mismatches.Count - 10} more");
}

var origFileSize = new FileInfo(origPath).Length;
var exportFileSize = new FileInfo(exportPath).Length;
var sizeDiff = Math.Abs((long)exportFileSize - (long)origFileSize);
var sizePercent = (double)sizeDiff / origFileSize * 100;

Console.WriteLine($"✓ File Size: {origFileSize} vs {exportFileSize} bytes (diff: {sizeDiff} bytes, {sizePercent:F1}%) {(sizePercent < 10 ? "✅ PASS" : "⚠️  WARN")}");

Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    NEXT STEPS                              ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

if (allTypesMatch && origStructCount == exportStructCount)
{
    Console.WriteLine("\n✅ Phase 1 Fix VALIDATED!");
    Console.WriteLine("\nReady for NWN Toolset testing:");
    Console.WriteLine("  1. Open lista_phase1_export.dlg in Aurora");
    Console.WriteLine("  2. Verify: Instant load (not 60 seconds)");
    Console.WriteLine("  3. Verify: Tree expands correctly");
    Console.WriteLine("  4. In-game test: No 'Debug' error");
}
else
{
    Console.WriteLine("\n❌ Phase 1 Fix INCOMPLETE");
    Console.WriteLine("   Struct types not fully preserved - check implementation");
}
