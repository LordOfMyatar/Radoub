// Export test files for in-game verification
using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Services;

class ExportForGameTest
{
    static async Task Main(string[] args)
    {
        string modulePath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG";

        var testFiles = new[]
        {
            ("lista.dlg", "__lista.dlg"),
            ("chef.dlg", "__chef.dlg"),
            ("myra_james.dlg", "__myra.dlg")
        };

        var service = new DialogFileService();
        int successCount = 0;

        Console.WriteLine("=== Exporting Files for In-Game Testing ===\n");

        foreach (var (sourceFile, targetFile) in testFiles)
        {
            string sourcePath = Path.Combine(modulePath, sourceFile);
            string targetPath = Path.Combine(modulePath, targetFile);

            try
            {
                Console.WriteLine($"Loading: {sourceFile}");

                if (!File.Exists(sourcePath))
                {
                    Console.WriteLine($"  ❌ Source file not found: {sourcePath}");
                    continue;
                }

                var dialog = await service.LoadFromFileAsync(sourcePath);
                if (dialog == null)
                {
                    Console.WriteLine($"  ❌ Parse returned null");
                    continue;
                }

                Console.WriteLine($"  Loaded: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");

                Console.WriteLine($"  Exporting to: {targetFile}");
                await service.SaveToFileAsync(dialog, targetPath);

                var fileInfo = new FileInfo(targetPath);
                Console.WriteLine($"  ✅ Exported: {fileInfo.Length} bytes");

                // Verify it loads back
                var reloaded = await service.LoadFromFileAsync(targetPath);
                if (reloaded != null)
                {
                    Console.WriteLine($"  ✅ Verified: {reloaded.Entries.Count} entries, {reloaded.Replies.Count} replies");
                    successCount++;
                }
                else
                {
                    Console.WriteLine($"  ❌ Verification failed: reloaded file is null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAILED: {ex.Message}");
            }

            Console.WriteLine();
        }

        Console.WriteLine($"=== Complete: {successCount}/{testFiles.Length} files exported successfully ===");

        if (successCount == testFiles.Length)
        {
            Console.WriteLine("\n✅ All files ready for in-game testing!");
            Console.WriteLine("Files exported:");
            foreach (var (_, targetFile) in testFiles)
            {
                Console.WriteLine($"  - {targetFile}");
            }
        }
    }
}
