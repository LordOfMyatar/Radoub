using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Parsers;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Automated Export Tool - Creating Fresh Test Files ===\n");

        string basePath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG";

        var testFiles = new[] {
            new { Original = "lista_new.dlg", Export = "__lista.dlg" },
            new { Original = "chef.dlg", Export = "__chef.dlg" },
            new { Original = "myra_james.dlg", Export = "__myra.dlg" },
            new { Original = "generic_hench.dlg", Export = "__hench.dlg" }
        };

        var parser = new DialogParser();

        foreach (var file in testFiles)
        {
            string origPath = Path.Combine(basePath, file.Original);
            string exportPath = Path.Combine(basePath, file.Export);

            if (!File.Exists(origPath))
            {
                Console.WriteLine($"❌ Original not found: {file.Original}");
                continue;
            }

            Console.WriteLine($"Processing {file.Original}...");

            try
            {
                // Load original
                var dialog = await parser.ParseFromFileAsync(origPath);

                if (dialog == null)
                {
                    Console.WriteLine($"❌ {file.Original}: Failed to parse");
                    continue;
                }

                Console.WriteLine($"  Loaded: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");

                // Export
                bool success = await parser.WriteToFileAsync(dialog, exportPath);

                if (success)
                {
                    var origSize = new FileInfo(origPath).Length;
                    var exportSize = new FileInfo(exportPath).Length;
                    var diff = exportSize - origSize;
                    var pct = Math.Round((diff / (double)origSize) * 100, 1);

                    Console.WriteLine($"  ✅ Exported to {file.Export}");
                    Console.WriteLine($"  Size: {exportSize:N0} bytes (original: {origSize:N0}, diff: {diff:+0;-0} bytes, {pct:+0.0;-0.0}%)\n");
                }
                else
                {
                    Console.WriteLine($"  ❌ Export failed\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}\n");
            }
        }

        Console.WriteLine("Check logs: C:~\\ArcReactor\\Logs\\Session_*\\Parser_*.log");
    }
}
