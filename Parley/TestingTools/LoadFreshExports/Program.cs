using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Services;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Loading Fresh Exported Files to Generate Logs ===\n");

        string basePath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG";
        string[] testFiles = { "lista_fresh.dlg", "chef_fresh.dlg", "myra_fresh.dlg", "hench_fresh.dlg" };

        var service = new DialogFileService();

        foreach (var file in testFiles)
        {
            string path = Path.Combine(basePath, file);
            if (!File.Exists(path)) { Console.WriteLine($"❌ Not found: {file}"); continue; }

            Console.WriteLine($"Loading {file}...");
            try
            {
                var dialog = await service.LoadFromFileAsync(path);
                if (dialog != null)
                {
                    Console.WriteLine($"✅ {file}: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies\n");
                }
                else
                {
                    Console.WriteLine($"❌ {file}: Returned null\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {file}: {ex.Message}\n");
            }
        }

        Console.WriteLine("Check logs: C:~\\ArcReactor\\Logs\\Session_*\\Parser_*.log");
    }
}
