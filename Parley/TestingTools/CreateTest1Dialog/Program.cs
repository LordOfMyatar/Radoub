using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;

namespace CreateTest1Dialog;

/// <summary>
/// Creates Test1_SharedReply.dlg for Issue #28 testing.
/// Test Case: IsLink flag preservation after delete → undo.
/// Structure: Entry1 → Reply1 ← Entry2 (where Entry2→Reply1 is a link)
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Creating test1_link.dlg...");
        Console.WriteLine("Test Case: Shared Reply with Link/Original structure");
        Console.WriteLine("(Aurora Engine compatible: 10-char filename)");

        // Create new dialog
        var dialog = new Dialog();

        // Entry 1: "What do you sell?" (no speaker tag - Aurora validates tags)
        var entry1 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry1.Text.Add(0, "What do you sell?");
        dialog.Entries.Add(entry1);

        // Reply 1: "I sell potions and healing supplies." (Shared reply)
        var reply1 = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString(),
            Speaker = ""
        };
        reply1.Text.Add(0, "I sell potions and healing supplies.");
        dialog.Replies.Add(reply1);

        // Pointer from Entry1 to Reply1 (ORIGINAL - IsLink=false)
        var ptrEntry1ToReply1 = new DialogPtr
        {
            Node = reply1,
            Type = DialogNodeType.Reply,
            Index = 0,
            IsLink = false,
            Parent = dialog
        };
        entry1.Pointers.Add(ptrEntry1ToReply1);

        // Entry 2: "Anything else for sale?" (no speaker tag)
        var entry2 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry2.Text.Add(0, "Anything else for sale?");
        dialog.Entries.Add(entry2);

        // Pointer from Entry2 to Reply1 (LINK - IsLink=true)
        var ptrEntry2ToReply1 = new DialogPtr
        {
            Node = reply1,
            Type = DialogNodeType.Reply,
            Index = 0,
            IsLink = true,
            Parent = dialog
        };
        entry2.Pointers.Add(ptrEntry2ToReply1);

        // Mark Entry1 and Entry2 as starting entries (conversation entry points)
        dialog.Starts.Add(new DialogPtr { Node = entry1, Type = DialogNodeType.Entry, Index = 0, IsLink = false, Parent = dialog });
        dialog.Starts.Add(new DialogPtr { Node = entry2, Type = DialogNodeType.Entry, Index = 1, IsLink = false, Parent = dialog });

        // Rebuild LinkRegistry to track all pointers
        dialog.RebuildLinkRegistry();

        // Save to file
        // Navigate from CreateTest1Dialog/bin/Debug/net9.0 up to Parley root
        // NOTE: Aurora Engine has 12-char filename limit (excluding .dlg extension)
        string outputPath = Path.Combine(
            Environment.CurrentDirectory,
            "..", "..", "..", "..", "..", "TestingTools", "TestFiles", "test1_link.dlg"
        );
        outputPath = Path.GetFullPath(outputPath);

        // Ensure directory exists
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileService = new DialogFileService();
        bool success = await fileService.SaveToFileAsync(dialog, outputPath);

        if (!success)
        {
            Console.WriteLine("❌ Failed to create dialog file!");
            return;
        }

        Console.WriteLine($"✅ Created: {outputPath}");
        Console.WriteLine();
        Console.WriteLine("Dialog Structure:");
        Console.WriteLine("  Entry1: \"What do you sell?\" (Merchant)");
        Console.WriteLine("    → Reply1: \"I sell potions...\" [ORIGINAL pointer] - ends conversation");
        Console.WriteLine();
        Console.WriteLine("  Entry2: \"Anything else for sale?\" (Merchant)");
        Console.WriteLine("    → Reply1: \"I sell potions...\" [LINK pointer] - ends conversation");
        Console.WriteLine();
        Console.WriteLine("Test Instructions:");
        Console.WriteLine("1. Open in Parley");
        Console.WriteLine("2. Expand tree, verify Entry2→Reply1 shows 'link' indicator");
        Console.WriteLine("3. Delete Entry2");
        Console.WriteLine("4. Press Ctrl+Z (Undo)");
        Console.WriteLine("5. VERIFY: Entry2→Reply1 still shows as LINK (not converted to original)");
        Console.WriteLine();
        Console.WriteLine("This tests Issue #28 fix: IsLink flags preserved during undo operations.");
    }
}
