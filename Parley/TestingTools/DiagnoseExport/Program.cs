using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Services;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: DiagnoseExport <path-to-dlg-file>");
            Console.WriteLine("  Loads DLG, exports it, then loads export and compares node counts");
            return;
        }

        string originalPath = args[0];
        if (!File.Exists(originalPath))
        {
            Console.WriteLine($"❌ File not found: {originalPath}");
            return;
        }

        string exportPath = Path.Combine(
            Path.GetDirectoryName(originalPath)!,
            "__" + Path.GetFileName(originalPath)
        );

        Console.WriteLine("=== Export Diagnostic Tool ===\n");
        Console.WriteLine($"Original: {originalPath}");
        Console.WriteLine($"Export:   {exportPath}\n");

        var service = new DialogFileService();
        var validator = new DialogValidator();

        try
        {
            // Load original
            Console.WriteLine("Loading original...");
            var original = await service.LoadFromFileAsync(originalPath);
            if (original == null)
            {
                Console.WriteLine("❌ Failed to load original");
                return;
            }

            Console.WriteLine($"✅ Original loaded:");
            Console.WriteLine($"   Entries: {original.Entries.Count}");
            Console.WriteLine($"   Replies: {original.Replies.Count}");
            Console.WriteLine($"   Starts:  {original.Starts.Count}");

            var origStats = validator.GetDialogStatistics(original);
            Console.WriteLine($"   Total Pointers: {origStats["TotalPointers"]}");

            // Validate original
            var origValidation = validator.ValidateStructure(original);
            if (!origValidation.Success)
            {
                Console.WriteLine($"⚠️  Original has validation errors: {origValidation.ErrorMessage}");
            }
            if (origValidation.Warnings.Count > 0)
            {
                Console.WriteLine($"⚠️  Original has {origValidation.Warnings.Count} warnings");
                foreach (var warning in origValidation.Warnings)
                {
                    Console.WriteLine($"     - {warning}");
                }
            }

            // Export
            Console.WriteLine("\nExporting...");
            bool exportSuccess = await service.SaveToFileAsync(original, exportPath);
            if (!exportSuccess)
            {
                Console.WriteLine("❌ Export failed");
                return;
            }

            var origSize = new FileInfo(originalPath).Length;
            var exportSize = new FileInfo(exportPath).Length;
            var diff = exportSize - origSize;
            var pct = Math.Round((diff / (double)origSize) * 100, 1);

            Console.WriteLine($"✅ Export succeeded");
            Console.WriteLine($"   Original size: {origSize:N0} bytes");
            Console.WriteLine($"   Export size:   {exportSize:N0} bytes");
            Console.WriteLine($"   Difference:    {diff:+0;-0} bytes ({pct:+0.0;-0.0}%)");

            if (Math.Abs(pct) > 5.0)
            {
                Console.WriteLine($"   ⚠️  WARNING: Size difference > 5% - possible data loss!");
            }

            // Load export
            Console.WriteLine("\nLoading export...");
            var exported = await service.LoadFromFileAsync(exportPath);
            if (exported == null)
            {
                Console.WriteLine("❌ Failed to load export");
                return;
            }

            Console.WriteLine($"✅ Export loaded:");
            Console.WriteLine($"   Entries: {exported.Entries.Count}");
            Console.WriteLine($"   Replies: {exported.Replies.Count}");
            Console.WriteLine($"   Starts:  {exported.Starts.Count}");

            var exportStats = validator.GetDialogStatistics(exported);
            Console.WriteLine($"   Total Pointers: {exportStats["TotalPointers"]}");

            // Validate export
            var exportValidation = validator.ValidateStructure(exported);
            if (!exportValidation.Success)
            {
                Console.WriteLine($"❌ Export has validation errors: {exportValidation.ErrorMessage}");
            }
            if (exportValidation.Warnings.Count > 0)
            {
                Console.WriteLine($"⚠️  Export has {exportValidation.Warnings.Count} warnings");
                foreach (var warning in exportValidation.Warnings)
                {
                    Console.WriteLine($"     - {warning}");
                }
            }

            // Compare
            Console.WriteLine("\n=== COMPARISON ===");
            bool hasIssues = false;

            if (original.Entries.Count != exported.Entries.Count)
            {
                Console.WriteLine($"❌ ENTRY COUNT MISMATCH: {original.Entries.Count} → {exported.Entries.Count} (lost {original.Entries.Count - exported.Entries.Count})");
                hasIssues = true;
            }

            if (original.Replies.Count != exported.Replies.Count)
            {
                Console.WriteLine($"❌ REPLY COUNT MISMATCH: {original.Replies.Count} → {exported.Replies.Count} (lost {original.Replies.Count - exported.Replies.Count})");
                hasIssues = true;
            }

            if (original.Starts.Count != exported.Starts.Count)
            {
                Console.WriteLine($"❌ START COUNT MISMATCH: {original.Starts.Count} → {exported.Starts.Count} (lost {original.Starts.Count - exported.Starts.Count})");
                hasIssues = true;
            }

            int origPointers = (int)origStats["TotalPointers"];
            int exportPointers = (int)exportStats["TotalPointers"];
            if (origPointers != exportPointers)
            {
                Console.WriteLine($"❌ POINTER COUNT MISMATCH: {origPointers} → {exportPointers} (lost {origPointers - exportPointers})");
                hasIssues = true;
            }

            if (!hasIssues)
            {
                Console.WriteLine("✅ All node counts match - round-trip successful!");
            }
            else
            {
                Console.WriteLine("\n❌ CRITICAL: Node loss detected during export!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Exception: {ex.Message}");
            Console.WriteLine($"   {ex.StackTrace}");
        }
    }
}
