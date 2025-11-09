using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;

namespace CreateTest2Dialog;

/// <summary>
/// Creates Test2_DeepNesting.dlg for Issue #28 testing.
/// Test Case: TreeView expansion preservation after undo.
/// Structure: Deep tree with 4+ levels of nesting for expansion state testing.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Creating test2_deep.dlg...");
        Console.WriteLine("Test Case: Deep tree structure for expansion state testing");
        Console.WriteLine("(Aurora Engine compatible: 10-char filename)");

        // Create new dialog
        var dialog = new Dialog();

        // Level 1: Entry 0 - "Greetings, traveler." (conversation entry point)
        var entry0 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry0.Text.Add(0, "Greetings, traveler. State your business.");
        dialog.Entries.Add(entry0);

        // Level 2: Reply 0 - "I'm just passing through."
        var reply0 = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString(),
            Speaker = ""
        };
        reply0.Text.Add(0, "I'm just passing through.");
        dialog.Replies.Add(reply0);

        var ptrEntry0ToReply0 = new DialogPtr
        {
            Node = reply0,
            Type = DialogNodeType.Reply,
            Index = 0,
            IsLink = false,
            Parent = dialog
        };
        entry0.Pointers.Add(ptrEntry0ToReply0);

        // Level 3: Entry 1 - "Very well. But stay out of trouble."
        var entry1 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry1.Text.Add(0, "Very well. But stay out of trouble.");
        dialog.Entries.Add(entry1);

        var ptrReply0ToEntry1 = new DialogPtr
        {
            Node = entry1,
            Type = DialogNodeType.Entry,
            Index = 1,
            IsLink = false,
            Parent = dialog
        };
        reply0.Pointers.Add(ptrReply0ToEntry1);

        // Level 4: Reply 1 - "I will, thank you."
        var reply1 = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString(),
            Speaker = ""
        };
        reply1.Text.Add(0, "I will, thank you.");
        dialog.Replies.Add(reply1);

        var ptrEntry1ToReply1 = new DialogPtr
        {
            Node = reply1,
            Type = DialogNodeType.Reply,
            Index = 1,
            IsLink = false,
            Parent = dialog
        };
        entry1.Pointers.Add(ptrEntry1ToReply1);

        // Level 5: Entry 2 - "Safe travels."
        var entry2 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry2.Text.Add(0, "Safe travels.");
        dialog.Entries.Add(entry2);

        var ptrReply1ToEntry2 = new DialogPtr
        {
            Node = entry2,
            Type = DialogNodeType.Entry,
            Index = 2,
            IsLink = false,
            Parent = dialog
        };
        reply1.Pointers.Add(ptrReply1ToEntry2);

        // Add a second branch at Level 2 for more interesting tree
        // Level 2: Reply 2 - "I seek information about the area."
        var reply2 = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString(),
            Speaker = ""
        };
        reply2.Text.Add(0, "I seek information about the area.");
        dialog.Replies.Add(reply2);

        var ptrEntry0ToReply2 = new DialogPtr
        {
            Node = reply2,
            Type = DialogNodeType.Reply,
            Index = 2,
            IsLink = false,
            Parent = dialog
        };
        entry0.Pointers.Add(ptrEntry0ToReply2);

        // Level 3: Entry 3 - "What do you want to know?"
        var entry3 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry3.Text.Add(0, "What do you want to know?");
        dialog.Entries.Add(entry3);

        var ptrReply2ToEntry3 = new DialogPtr
        {
            Node = entry3,
            Type = DialogNodeType.Entry,
            Index = 3,
            IsLink = false,
            Parent = dialog
        };
        reply2.Pointers.Add(ptrReply2ToEntry3);

        // Level 4: Reply 3 - "Are there any dangers nearby?"
        var reply3 = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString(),
            Speaker = ""
        };
        reply3.Text.Add(0, "Are there any dangers nearby?");
        dialog.Replies.Add(reply3);

        var ptrEntry3ToReply3 = new DialogPtr
        {
            Node = reply3,
            Type = DialogNodeType.Reply,
            Index = 3,
            IsLink = false,
            Parent = dialog
        };
        entry3.Pointers.Add(ptrEntry3ToReply3);

        // Level 5: Entry 4 - "Bandits have been spotted to the north. Be careful."
        var entry4 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry4.Text.Add(0, "Bandits have been spotted to the north. Be careful.");
        dialog.Entries.Add(entry4);

        var ptrReply3ToEntry4 = new DialogPtr
        {
            Node = entry4,
            Type = DialogNodeType.Entry,
            Index = 4,
            IsLink = false,
            Parent = dialog
        };
        reply3.Pointers.Add(ptrReply3ToEntry4);

        // Level 6: Reply 4 - "Thanks for the warning."
        var reply4 = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString(),
            Speaker = ""
        };
        reply4.Text.Add(0, "Thanks for the warning.");
        dialog.Replies.Add(reply4);

        var ptrEntry4ToReply4 = new DialogPtr
        {
            Node = reply4,
            Type = DialogNodeType.Reply,
            Index = 4,
            IsLink = false,
            Parent = dialog
        };
        entry4.Pointers.Add(ptrEntry4ToReply4);

        // Mark Entry0 as starting entry (conversation entry point)
        dialog.Starts.Add(new DialogPtr { Node = entry0, Type = DialogNodeType.Entry, Index = 0, IsLink = false, Parent = dialog });

        // Rebuild LinkRegistry
        dialog.RebuildLinkRegistry();

        // Save to file
        // Navigate from CreateTest2Dialog/bin/Debug/net9.0 up to Parley root
        // NOTE: Aurora Engine has 12-char filename limit (excluding .dlg extension)
        string outputPath = Path.Combine(
            Environment.CurrentDirectory,
            "..", "..", "..", "..", "..", "TestingTools", "TestFiles", "test2_deep.dlg"
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
        Console.WriteLine("Dialog Structure (6 levels deep):");
        Console.WriteLine("  Entry0: \"Greetings, traveler. State your business.\" (Guard)");
        Console.WriteLine("    → Reply0: \"I'm just passing through.\"");
        Console.WriteLine("      → Entry1: \"Very well. But stay out of trouble.\" (Guard)");
        Console.WriteLine("        → Reply1: \"I will, thank you.\"");
        Console.WriteLine("          → Entry2: \"Safe travels.\" (Guard)");
        Console.WriteLine();
        Console.WriteLine("    → Reply2: \"I seek information about the area.\"");
        Console.WriteLine("      → Entry3: \"What do you want to know?\" (Guard)");
        Console.WriteLine("        → Reply3: \"Are there any dangers nearby?\"");
        Console.WriteLine("          → Entry4: \"Bandits have been spotted to the north...\" (Guard)");
        Console.WriteLine("            → Reply4: \"Thanks for the warning.\"");
        Console.WriteLine();
        Console.WriteLine("Test Instructions:");
        Console.WriteLine("1. Open in Parley");
        Console.WriteLine("2. Expand ALL nodes to full depth (click expand icons)");
        Console.WriteLine("3. Select Entry3 (middle-level node: 'What do you want to know?')");
        Console.WriteLine("4. Delete Entry3 (Del key) - removes Entry3, Reply3, Entry4, Reply4");
        Console.WriteLine("5. Press Ctrl+Z (Undo)");
        Console.WriteLine("6. VERIFY: All nodes still expanded as before delete");
        Console.WriteLine("7. VERIFY: Entire subtree restored with correct expansion state");
        Console.WriteLine();
        Console.WriteLine("This tests Issue #28 fix: TreeView expansion preserved during undo.");
    }
}
