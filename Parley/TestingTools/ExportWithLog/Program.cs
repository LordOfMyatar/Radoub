using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Services;
using DialogEditor.Services;

class Program
{
    static async Task Main()
    {
        // Enable DEBUG logging to see all fieldData traces
        UnifiedLogger.SetLogLevel(LogLevel.DEBUG);

        string originalFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";
        string exportFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\__chef.dlg";

        var service = new DialogFileService();

        Console.WriteLine($"Loading: {originalFile}");
        var dialog = await service.LoadFromFileAsync(originalFile);

        if (dialog == null)
        {
            Console.WriteLine("Failed to load file!");
            return;
        }

        Console.WriteLine($"Loaded: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");
        Console.WriteLine($"Exporting to: {exportFile}");

        await service.SaveToFileAsync(dialog, exportFile);

        Console.WriteLine($"\n✅ Export complete!");
        Console.WriteLine($"Check logs at: {UnifiedLogger.GetSessionDirectory()}");
    }
}
