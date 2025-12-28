using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Services;
using DialogEditor.Models;

namespace DeepStructAnalyzer;

class Program
{
    static async Task Main(string[] args)
    {
        string origPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";
        string exportPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_conditionalquest_test.dlg";

        Console.WriteLine("=== DEEP STRUCT ORDERING ANALYSIS ===\n");

        // Parse both files
        var service = new DialogFileService();
        var originalDialog = await service.LoadFromFileAsync(origPath);
        var exportedDialog = await service.LoadFromFileAsync(exportPath);

        Console.WriteLine($"Original Conversation Structure:");
        Console.WriteLine($"  Entries: {originalDialog.Entries.Count}");
        Console.WriteLine($"  Replies: {originalDialog.Replies.Count}");
        Console.WriteLine($"  Starts: {originalDialog.Starts.Count}");
        Console.WriteLine();

        // Analyze conversation flow from Starts
        Console.WriteLine("=== CONVERSATION FLOW ANALYSIS (from Starts) ===\n");
        AnalyzeConversationFlow(originalDialog);

        Console.WriteLine("\n=== BREADTH-FIRST TRAVERSAL ORDER ===\n");
        BreadthFirstTraversal(originalDialog);

        Console.WriteLine("\n=== DEPTH-FIRST TRAVERSAL ORDER (current implementation) ===\n");
        DepthFirstTraversal(originalDialog);

        Console.WriteLine("\n=== INDEX-SORTED TRAVERSAL ORDER ===\n");
        IndexSortedTraversal(originalDialog);
    }

    static void AnalyzeConversationFlow(Dialog dialog)
    {
        Console.WriteLine("Starting nodes:");
        foreach (var start in dialog.Starts)
        {
            Console.WriteLine($"  Start → Entry[{start.Index}]");
        }
        Console.WriteLine();

        Console.WriteLine("Entry → Reply connections:");
        for (int i = 0; i < dialog.Entries.Count; i++)
        {
            var entry = dialog.Entries[i];
            string text = entry.Text?.Strings.FirstOrDefault().Value ?? "";
            Console.Write($"  Entry[{i}]: \"{text.Substring(0, Math.Min(20, text.Length))}...\" → ");
            if (entry.Pointers.Count == 0)
            {
                Console.WriteLine("[END]");
            }
            else
            {
                Console.WriteLine($"Replies[{string.Join(", ", entry.Pointers.Select(p => p.Index))}]");
            }
        }
        Console.WriteLine();

        Console.WriteLine("Reply → Entry connections:");
        for (int i = 0; i < dialog.Replies.Count; i++)
        {
            var reply = dialog.Replies[i];
            string text = reply.Text?.Strings.FirstOrDefault().Value ?? "";
            Console.Write($"  Reply[{i}]: \"{text.Substring(0, Math.Min(20, text.Length))}...\" → ");
            if (reply.Pointers.Count == 0)
            {
                Console.WriteLine("[END]");
            }
            else
            {
                Console.WriteLine($"Entries[{string.Join(", ", reply.Pointers.Select(p => p.Index))}]");
            }
        }
    }

    static void BreadthFirstTraversal(Dialog dialog)
    {
        var visitedEntries = new HashSet<uint>();
        var visitedReplies = new HashSet<uint>();
        var queue = new Queue<(string type, uint index)>();

        Console.WriteLine("Traversal order (Root → structs in breadth-first):");
        Console.WriteLine("  0: Root struct");

        // Add all starts to queue
        foreach (var start in dialog.Starts)
        {
            queue.Enqueue(("entry", start.Index));
        }

        int structNum = 1;
        while (queue.Count > 0)
        {
            var (type, index) = queue.Dequeue();

            if (type == "entry")
            {
                if (visitedEntries.Contains(index)) continue;
                visitedEntries.Add(index);

                var entry = dialog.Entries[(int)index];
                Console.WriteLine($"  {structNum++}: Entry[{index}] - {entry.Pointers.Count} pointers");

                // Add pointer structs
                foreach (var ptr in entry.Pointers)
                {
                    Console.WriteLine($"  {structNum++}: Entry[{index}].Pointer → Reply[{ptr.Index}]");
                }

                // Queue replies for next level
                foreach (var ptr in entry.Pointers)
                {
                    queue.Enqueue(("reply", ptr.Index));
                }
            }
            else if (type == "reply")
            {
                if (visitedReplies.Contains(index)) continue;
                visitedReplies.Add(index);

                var reply = dialog.Replies[(int)index];
                Console.WriteLine($"  {structNum++}: Reply[{index}] - {reply.Pointers.Count} pointers");

                // Add pointer structs
                foreach (var ptr in reply.Pointers)
                {
                    Console.WriteLine($"  {structNum++}: Reply[{index}].Pointer → Entry[{ptr.Index}]");
                }

                // Queue entries for next level
                foreach (var ptr in reply.Pointers)
                {
                    queue.Enqueue(("entry", ptr.Index));
                }
            }
        }

        // Add Start structs at end
        for (int i = 0; i < dialog.Starts.Count; i++)
        {
            Console.WriteLine($"  {structNum++}: Start[{i}] → Entry[{dialog.Starts[i].Index}]");
        }
    }

    static void DepthFirstTraversal(Dialog dialog)
    {
        var visitedEntries = new HashSet<uint>();
        var visitedReplies = new HashSet<uint>();
        int structNum = 1;

        Console.WriteLine("Traversal order (Root → structs in depth-first):");
        Console.WriteLine("  0: Root struct");

        void TraverseEntry(uint entryIndex, int depth)
        {
            if (visitedEntries.Contains(entryIndex)) return;
            visitedEntries.Add(entryIndex);

            var entry = dialog.Entries[(int)entryIndex];
            Console.WriteLine($"  {structNum++}: Entry[{entryIndex}] - {entry.Pointers.Count} pointers (depth {depth})");

            foreach (var ptr in entry.Pointers)
            {
                Console.WriteLine($"  {structNum++}: Entry[{entryIndex}].Pointer → Reply[{ptr.Index}]");
                TraverseReply(ptr.Index, depth + 1);
            }
        }

        void TraverseReply(uint replyIndex, int depth)
        {
            if (replyIndex >= dialog.Replies.Count) return;
            if (visitedReplies.Contains(replyIndex)) return;
            visitedReplies.Add(replyIndex);

            var reply = dialog.Replies[(int)replyIndex];
            Console.WriteLine($"  {structNum++}: Reply[{replyIndex}] - {reply.Pointers.Count} pointers (depth {depth})");

            foreach (var ptr in reply.Pointers)
            {
                Console.WriteLine($"  {structNum++}: Reply[{replyIndex}].Pointer → Entry[{ptr.Index}]");
                TraverseEntry(ptr.Index, depth + 1);
            }
        }

        foreach (var start in dialog.Starts)
        {
            TraverseEntry(start.Index, 0);
        }

        // Add Start structs at end
        for (int i = 0; i < dialog.Starts.Count; i++)
        {
            Console.WriteLine($"  {structNum++}: Start[{i}] → Entry[{dialog.Starts[i].Index}]");
        }
    }

    static void IndexSortedTraversal(Dialog dialog)
    {
        int structNum = 1;

        Console.WriteLine("Traversal order (sorted by Index field):");
        Console.WriteLine("  0: Root struct");

        // Collect all nodes with their indices
        var nodes = new List<(string type, uint index, int pointerCount)>();

        for (uint i = 0; i < dialog.Entries.Count; i++)
        {
            nodes.Add(("Entry", i, dialog.Entries[(int)i].Pointers.Count));
        }

        for (uint i = 0; i < dialog.Replies.Count; i++)
        {
            nodes.Add(("Reply", i, dialog.Replies[(int)i].Pointers.Count));
        }

        // Sort by index
        nodes.Sort((a, b) => a.index.CompareTo(b.index));

        foreach (var (type, index, pointerCount) in nodes)
        {
            Console.WriteLine($"  {structNum++}: {type}[{index}] - {pointerCount} pointers");

            if (type == "Entry")
            {
                var entry = dialog.Entries[(int)index];
                foreach (var ptr in entry.Pointers)
                {
                    Console.WriteLine($"  {structNum++}: {type}[{index}].Pointer → Reply[{ptr.Index}]");
                }
            }
            else
            {
                var reply = dialog.Replies[(int)index];
                foreach (var ptr in reply.Pointers)
                {
                    Console.WriteLine($"  {structNum++}: {type}[{index}].Pointer → Entry[{ptr.Index}]");
                }
            }
        }

        // Add Start structs at end
        for (int i = 0; i < dialog.Starts.Count; i++)
        {
            Console.WriteLine($"  {structNum++}: Start[{i}] → Entry[{dialog.Starts[i].Index}]");
        }
    }
}
