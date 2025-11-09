using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;

namespace CompareDialogs;

/// <summary>
/// Compares structure of known-good dialog vs generated test dialog
/// to identify what makes Aurora Engine fail to load our generated files.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Dialog Structure Comparison ===");
        Console.WriteLine();

        // Load known-good dialog
        string goodPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Neverwinter Nights", "modules", "LNS_DLG", "dlg_shady_vendor.dlg");
        Console.WriteLine($"Loading known-good: {Path.GetFileName(goodPath)}");

        var fileService = new DialogFileService();
        var goodDialog = await fileService.LoadFromFileAsync(goodPath);

        if (goodDialog == null)
        {
            Console.WriteLine("❌ Failed to load known-good dialog!");
            return;
        }

        Console.WriteLine("✅ Loaded known-good dialog");
        PrintDialogStats("KNOWN-GOOD (dlg_shady_vendor.dlg)", goodDialog);

        Console.WriteLine();
        Console.WriteLine("---");
        Console.WriteLine();

        // Load generated test dialog
        string testPath = @"D:\LOM\TestingTools\TestFiles\test1_link.dlg";
        Console.WriteLine($"Loading generated test: {Path.GetFileName(testPath)}");

        var testDialog = await fileService.LoadFromFileAsync(testPath);

        if (testDialog == null)
        {
            Console.WriteLine("❌ Failed to load test dialog!");
            return;
        }

        Console.WriteLine("✅ Loaded test dialog");
        PrintDialogStats("GENERATED (test1_link.dlg)", testDialog);

        Console.WriteLine();
        Console.WriteLine("---");
        Console.WriteLine();
        Console.WriteLine("=== KEY DIFFERENCES ===");
        Console.WriteLine();

        // Compare starting entries
        Console.WriteLine($"Starting Entries:");
        Console.WriteLine($"  Good: {goodDialog.Starts.Count}");
        Console.WriteLine($"  Test: {testDialog.Starts.Count}");

        if (goodDialog.Starts.Count > 0)
        {
            Console.WriteLine($"  Good Start[0]: Index={goodDialog.Starts[0].Index}, IsLink={goodDialog.Starts[0].IsLink}");
        }
        if (testDialog.Starts.Count > 0)
        {
            Console.WriteLine($"  Test Start[0]: Index={testDialog.Starts[0].Index}, IsLink={testDialog.Starts[0].IsLink}");
        }

        Console.WriteLine();

        // Compare entries
        Console.WriteLine($"Entry Structure:");
        for (int i = 0; i < Math.Min(3, Math.Min(goodDialog.Entries.Count, testDialog.Entries.Count)); i++)
        {
            Console.WriteLine($"  Entry {i}:");
            if (i < goodDialog.Entries.Count)
            {
                var ge = goodDialog.Entries[i];
                Console.WriteLine($"    Good: Speaker='{ge.Speaker}', Text='{GetFirstText(ge.Text)}', Pointers={ge.Pointers.Count}");
            }
            if (i < testDialog.Entries.Count)
            {
                var te = testDialog.Entries[i];
                Console.WriteLine($"    Test: Speaker='{te.Speaker}', Text='{GetFirstText(te.Text)}', Pointers={te.Pointers.Count}");
            }
        }

        Console.WriteLine();

        // Compare replies
        Console.WriteLine($"Reply Structure:");
        for (int i = 0; i < Math.Min(3, Math.Min(goodDialog.Replies.Count, testDialog.Replies.Count)); i++)
        {
            Console.WriteLine($"  Reply {i}:");
            if (i < goodDialog.Replies.Count)
            {
                var gr = goodDialog.Replies[i];
                Console.WriteLine($"    Good: Speaker='{gr.Speaker}', Text='{GetFirstText(gr.Text)}', Pointers={gr.Pointers.Count}");
            }
            if (i < testDialog.Replies.Count)
            {
                var tr = testDialog.Replies[i];
                Console.WriteLine($"    Test: Speaker='{tr.Speaker}', Text='{GetFirstText(tr.Text)}', Pointers={tr.Pointers.Count}");
            }
        }
    }

    static void PrintDialogStats(string label, Dialog dialog)
    {
        Console.WriteLine($"{label}:");
        Console.WriteLine($"  Entries: {dialog.Entries.Count}");
        Console.WriteLine($"  Replies: {dialog.Replies.Count}");
        Console.WriteLine($"  Starting Entries: {dialog.Starts.Count}");

        int totalEntryPointers = 0;
        int totalReplyPointers = 0;

        foreach (var entry in dialog.Entries)
        {
            totalEntryPointers += entry.Pointers.Count;
        }

        foreach (var reply in dialog.Replies)
        {
            totalReplyPointers += reply.Pointers.Count;
        }

        Console.WriteLine($"  Total Entry Pointers: {totalEntryPointers}");
        Console.WriteLine($"  Total Reply Pointers: {totalReplyPointers}");
    }

    static string GetFirstText(LocString locString)
    {
        if (locString == null || locString.Strings.Count == 0)
            return "(empty)";

        // LocString.Strings is Dictionary<int, string>
        var firstValue = locString.Strings.Values.First();
        if (firstValue.Length > 40)
            return firstValue.Substring(0, 40) + "...";
        return firstValue;
    }
}
