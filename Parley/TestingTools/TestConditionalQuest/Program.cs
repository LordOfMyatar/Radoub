using DialogEditor.Services;
using System;
using System.IO;

string origPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";
string exportPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_conditionalquest_test.dlg";

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║   TESTING CONDITIONAL QUESTENTRY FIELD FIX                ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

Console.WriteLine($"Loading: {origPath}");
var service = new DialogFileService();
var dialog = await service.LoadFromFileAsync(origPath);

if (dialog == null)
{
    Console.WriteLine("✗ Failed to load dialog");
    return;
}

Console.WriteLine($"✓ Loaded dialog with {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");

Console.WriteLine($"\nExporting: {exportPath}");
await service.SaveToFileAsync(dialog, exportPath);

var origInfo = new FileInfo(origPath);
var exportInfo = new FileInfo(exportPath);

Console.WriteLine($"\n╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine($"║   FILE SIZE COMPARISON                                    ║");
Console.WriteLine($"╚═══════════════════════════════════════════════════════════╝");
Console.WriteLine($"Original:  {origInfo.Length,6} bytes");
Console.WriteLine($"Exported:  {exportInfo.Length,6} bytes");
Console.WriteLine($"Difference: {exportInfo.Length - origInfo.Length,5} bytes ({((exportInfo.Length - origInfo.Length) / (double)origInfo.Length * 100):F1}%)");

Console.WriteLine($"\n✓ Export complete");
Console.WriteLine($"\nNext: Run Phase1Validator to check struct types and counts");
