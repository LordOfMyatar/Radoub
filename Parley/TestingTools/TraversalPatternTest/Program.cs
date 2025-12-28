using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Services;
using DialogEditor.Models;

namespace TraversalPatternTest;

class Program
{
    static async Task Main(string[] args)
    {
        string origPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";

        var service = new DialogFileService();
        var dialog = await service.LoadFromFileAsync(origPath);

        Console.WriteLine("=== CONVERSATION STRUCTURE ===\n");
        Console.WriteLine($"Entries: {dialog.Entries.Count}");
        Console.WriteLine($"Replies: {dialog.Replies.Count}");
        Console.WriteLine($"Starts: {dialog.Starts.Count}");
        Console.WriteLine();

        Console.WriteLine("Entry details:");
        for (int i = 0; i < dialog.Entries.Count; i++)
        {
            var entry = dialog.Entries[i];
            var text = entry.Text?.GetDefault() ?? "";
            Console.WriteLine($"  Entry[{i}]: \"{text.Substring(0, Math.Min(30, text.Length))}...\"");
            Console.WriteLine($"    Pointers: {entry.Pointers.Count} → [{string.Join(", ", entry.Pointers.Select(p => $"Reply[{p.Index}]"))}]");
            Console.WriteLine($"    OriginalGffStruct Type: {entry.OriginalGffStruct?.Type}");
        }
        Console.WriteLine();

        Console.WriteLine("Reply details:");
        for (int i = 0; i < dialog.Replies.Count; i++)
        {
            var reply = dialog.Replies[i];
            var text = reply.Text?.GetDefault() ?? "";
            Console.WriteLine($"  Reply[{i}]: \"{text.Substring(0, Math.Min(30, text.Length))}...\"");
            Console.WriteLine($"    Pointers: {reply.Pointers.Count} → [{string.Join(", ", reply.Pointers.Select(p => $"Entry[{p.Index}]"))}]");
            Console.WriteLine($"    Quest: '{reply.Quest}'");
            Console.WriteLine($"    OriginalGffStruct Type: {reply.OriginalGffStruct?.Type}");
        }
        Console.WriteLine();

        Console.WriteLine("=== TESTING DIFFERENT TRAVERSAL ORDERS ===\n");

        // Test 1: Pointer order (current implementation)
        Console.WriteLine("Test 1: Follow pointers in POINTER ORDER (Replies[2, 0])");
        Console.WriteLine("  1. Entry[0]");
        Console.WriteLine("  2. Entry[0].Pointer[0] → Reply[2]");
        Console.WriteLine("  3. Entry[0].Pointer[1] → Reply[0]");
        Console.WriteLine("  4. Reply[2] ← First target");
        Console.WriteLine("  5. Reply[0] ← Second target");
        Console.WriteLine();

        // Test 2: Index-sorted order
        Console.WriteLine("Test 2: Follow pointers in INDEX ORDER (sort [2, 0] → [0, 2])");
        Console.WriteLine("  1. Entry[0]");
        Console.WriteLine("  2. Entry[0].Pointer[0] → Reply[2]");
        Console.WriteLine("  3. Entry[0].Pointer[1] → Reply[0]");
        Console.WriteLine("  4. Reply[0] ← Lowest index first");
        Console.WriteLine("  5. Reply[2] ← Higher index second");
        Console.WriteLine();

        // Test 3: Check if Aurora's Struct[4] could be Reply[0]
        var reply0 = dialog.Replies[0];
        var reply2 = dialog.Replies[2];

        Console.WriteLine("Reply[0] characteristics:");
        Console.WriteLine($"  Text: {reply0.Text?.GetDefault()}");
        Console.WriteLine($"  Quest: '{reply0.Quest}' (empty: {string.IsNullOrEmpty(reply0.Quest)})");
        Console.WriteLine($"  Expected field count: {(string.IsNullOrEmpty(reply0.Quest) ? 10 : 11)}");
        Console.WriteLine($"  Pointers: {reply0.Pointers.Count}");
        Console.WriteLine();

        Console.WriteLine("Reply[2] characteristics:");
        Console.WriteLine($"  Text: {reply2.Text?.GetDefault()}");
        Console.WriteLine($"  Quest: '{reply2.Quest}' (empty: {string.IsNullOrEmpty(reply2.Quest)})");
        Console.WriteLine($"  Expected field count: {(string.IsNullOrEmpty(reply2.Quest) ? 10 : 11)}");
        Console.WriteLine($"  Pointers: {reply2.Pointers.Count}");
        Console.WriteLine();

        Console.WriteLine("=== AURORA STRUCT[4] ANALYSIS ===");
        Console.WriteLine("Aurora Struct[4]: Type=1, FieldCount=11");
        Console.WriteLine();
        Console.WriteLine("If Struct[4] is Reply[0]:");
        Console.WriteLine($"  → Reply[0] needs 11 fields (has Quest? {!string.IsNullOrEmpty(reply0.Quest)})");
        Console.WriteLine($"  → Reply[0] field count: {(string.IsNullOrEmpty(reply0.Quest) ? 10 : 11)} {(string.IsNullOrEmpty(reply0.Quest) ? "❌" : "✅")}");
        Console.WriteLine();
        Console.WriteLine("If Struct[4] is Reply[2]:");
        Console.WriteLine($"  → Reply[2] needs 11 fields (has Quest? {!string.IsNullOrEmpty(reply2.Quest)})");
        Console.WriteLine($"  → Reply[2] field count: {(string.IsNullOrEmpty(reply2.Quest) ? 10 : 11)} {(string.IsNullOrEmpty(reply2.Quest) ? "❌" : "✅")}");
        Console.WriteLine();

        Console.WriteLine("=== HYPOTHESIS ===");
        if (string.IsNullOrEmpty(reply0.Quest) && string.IsNullOrEmpty(reply2.Quest))
        {
            Console.WriteLine("❌ Neither Reply[0] nor Reply[2] has Quest - both would be 10 fields");
            Console.WriteLine("Aurora Struct[4] with 11 fields suggests it's an Entry or a Reply WITH Quest");
            Console.WriteLine();
            Console.WriteLine("Possibility: Aurora might traverse Entry[1] or Entry[2] before Replies");
        }
        else if (!string.IsNullOrEmpty(reply0.Quest))
        {
            Console.WriteLine("✅ Reply[0] HAS Quest - would be 11 fields");
            Console.WriteLine("→ Hypothesis: Aurora follows pointers in INDEX ORDER (0 before 2)");
            Console.WriteLine("→ Test: Change traversal to sort pointer targets by Index");
        }
        else if (!string.IsNullOrEmpty(reply2.Quest))
        {
            Console.WriteLine("✅ Reply[2] HAS Quest - would be 11 fields");
            Console.WriteLine("→ Matches current pointer order traversal");
            Console.WriteLine("→ Problem must be elsewhere (struct types preservation?)");
        }
    }
}
