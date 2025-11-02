using System;
using System.Threading.Tasks;
using DialogEditor.Parsers;
using DialogEditor.Services;
using TestingTools.TestFiles;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Testing Exported File Parsing ===");

        var parser = new DialogParser();

        // Test original file
        string originalFile = TestPathHelper.GetTestFilePath("chef.dlg");
        Console.WriteLine($"\n--- Testing Original File: {UnifiedLogger.SanitizePath(originalFile)} ---");
        try
        {
            var originalDialog = await parser.ParseFromFileAsync(originalFile);
            if (originalDialog != null)
            {
                Console.WriteLine($"✅ Original parsed successfully:");
                Console.WriteLine($"   Entries: {originalDialog.Entries.Count}");
                Console.WriteLine($"   Replies: {originalDialog.Replies.Count}");
                Console.WriteLine($"   Starts: {originalDialog.Starts.Count}");

                // Show first few entries for verification
                if (originalDialog.Entries.Count > 0)
                {
                    Console.WriteLine($"   First entry text: '{originalDialog.Entries[0].DisplayText}'");
                }
            }
            else
            {
                Console.WriteLine("❌ Original file returned null dialog");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error parsing original: {ex.Message}");
        }

        // Test exported file
        string exportedFile = TestPathHelper.GetTestFilePath("chef_exported.dlg");
        Console.WriteLine($"\n--- Testing Exported File: {UnifiedLogger.SanitizePath(exportedFile)} ---");
        try
        {
            var exportedDialog = await parser.ParseFromFileAsync(exportedFile);
            if (exportedDialog != null)
            {
                Console.WriteLine($"✅ Exported parsed successfully:");
                Console.WriteLine($"   Entries: {exportedDialog.Entries.Count}");
                Console.WriteLine($"   Replies: {exportedDialog.Replies.Count}");
                Console.WriteLine($"   Starts: {exportedDialog.Starts.Count}");

                // Show first few entries for verification
                if (exportedDialog.Entries.Count > 0)
                {
                    Console.WriteLine($"   First entry text: '{exportedDialog.Entries[0].DisplayText}'");
                }

                // Check if starts are properly linked
                Console.WriteLine("\n--- Start Entry Analysis ---");
                for (int i = 0; i < exportedDialog.Starts.Count; i++)
                {
                    var start = exportedDialog.Starts[i];
                    Console.WriteLine($"   Start[{i}]: Index={start.Index}, Node={(start.Node != null ? "LINKED" : "NULL")}");
                    if (start.Node != null)
                    {
                        Console.WriteLine($"      -> '{start.Node.DisplayText}'");
                    }
                }
            }
            else
            {
                Console.WriteLine("❌ Exported file returned null dialog");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error parsing exported: {ex.Message}");
        }

        Console.WriteLine("\n=== Test Complete ===");
    }
}
