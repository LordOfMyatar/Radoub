using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Services;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Struct Mapping Diagnostic Tool ===\n");

        string filePath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";

        var service = new DialogFileService();

        Console.WriteLine($"Loading: {filePath}");
        var dialog = await service.LoadFromFileAsync(filePath);

        if (dialog == null)
        {
            Console.WriteLine("Failed to load file!");
            return;
        }

        Console.WriteLine($"\nLoaded: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies\n");

        // Now re-export to see struct creation order
        string exportPath = Path.GetTempFileName() + ".dlg";
        Console.WriteLine($"Exporting to: {exportPath}");
        bool success = await service.SaveToFileAsync(dialog, exportPath);

        if (!success)
        {
            Console.WriteLine("Export failed!");
            return;
        }

        Console.WriteLine($"\n✅ Export successful");
        Console.WriteLine($"\nCheck logs for struct creation details:");
        Console.WriteLine($"C:~\\ArcReactor\\Logs\\Session_*\\Parser_*.log");

        // Clean up temp file
        if (File.Exists(exportPath))
            File.Delete(exportPath);
    }
}
