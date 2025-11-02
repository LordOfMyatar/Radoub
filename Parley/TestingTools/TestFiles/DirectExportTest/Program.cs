using System;
using System.Threading.Tasks;
using DialogEditor.Parsers;
using DialogEditor.Services;
using TestingTools.TestFiles;

class DirectExportTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Direct Export Test ===");

        // Set log level to see field count details
        UnifiedLogger.SetLogLevel(LogLevel.DEBUG);

        var parser = new DialogParser();

        // Load chef.dlg using workspace-relative paths
        string originalFile = TestPathHelper.GetTestFilePath("chef.dlg");
        string testFilesDir = TestPathHelper.GetTestFilesDir();
        Console.WriteLine($"Loading: {UnifiedLogger.SanitizePath(originalFile)}");

        var dialog = await parser.ParseFromFileAsync(originalFile);
        if (dialog == null)
        {
            Console.WriteLine("❌ Failed to load original file");
            return;
        }

        Console.WriteLine($"✅ Loaded: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies, {dialog.Starts.Count} starts");

        // Export with timestamp
        string timestamp = DateTime.Now.ToString("HHmmss");
        string exportFile = Path.Combine(testFilesDir, $"chef_test_{timestamp}.dlg");
        Console.WriteLine($"Exporting to: {UnifiedLogger.SanitizePath(exportFile)}");

        bool success = await parser.WriteToFileAsync(dialog, exportFile);
        Console.WriteLine($"Export result: {(success ? "SUCCESS" : "FAILED")}");

        if (success)
        {
            // Test parsing the exported file
            Console.WriteLine("\n--- Testing exported file ---");
            var exportedDialog = await parser.ParseFromFileAsync(exportFile);
            if (exportedDialog != null)
            {
                Console.WriteLine($"✅ Exported file parsed: {exportedDialog.Entries.Count} entries, {exportedDialog.Replies.Count} replies, {exportedDialog.Starts.Count} starts");

                if (exportedDialog.Entries.Count == dialog.Entries.Count &&
                    exportedDialog.Replies.Count == dialog.Replies.Count &&
                    exportedDialog.Starts.Count == dialog.Starts.Count)
                {
                    Console.WriteLine("🎉 SUCCESS: Field counts match!");
                }
                else
                {
                    Console.WriteLine("❌ FAILED: Field counts don't match");
                }
            }
            else
            {
                Console.WriteLine("❌ Failed to parse exported file");
            }
        }

        Console.WriteLine("\n=== Test Complete ===");
    }
}
