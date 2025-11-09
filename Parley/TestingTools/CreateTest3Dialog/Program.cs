using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;

namespace CreateTest3Dialog;

/// <summary>
/// Creates Test3_MultipleLinks.dlg for Issue #28 testing.
/// Test Case: Complex undo/redo cycle with multiple link/original combinations.
/// Structure: Multiple shared nodes with different IsLink flag patterns.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Creating test3_multi.dlg...");
        Console.WriteLine("Test Case: Complex link structure for undo/redo cycle testing");
        Console.WriteLine("(Aurora Engine compatible: 11-char filename)");

        // Create new dialog
        var dialog = new Dialog();

        // Entry 0: "Welcome to the shop!"
        var entry0 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry0.Text.Add(0, "Welcome to the shop!");
        dialog.Entries.Add(entry0);

        // Reply 0: "What are you selling?" (Target X - shared 3 times)
        var reply0 = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString(),
            Speaker = ""
        };
        reply0.Text.Add(0, "What are you selling?");
        dialog.Replies.Add(reply0);

        // Entry0 → Reply0 (ORIGINAL)
        var ptrEntry0ToReply0 = new DialogPtr
        {
            Node = reply0,
            Type = DialogNodeType.Reply,
            Index = 0,
            IsLink = false,
            Parent = dialog
        };
        entry0.Pointers.Add(ptrEntry0ToReply0);

        // Entry 1: "Prices are negotiable."
        var entry1 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry1.Text.Add(0, "Prices are negotiable.");
        dialog.Entries.Add(entry1);

        // Reply0 → Entry1
        var ptrReply0ToEntry1 = new DialogPtr
        {
            Node = entry1,
            Type = DialogNodeType.Entry,
            Index = 1,
            IsLink = false,
            Parent = dialog
        };
        reply0.Pointers.Add(ptrReply0ToEntry1);

        // Entry 2: "I also buy rare items."
        var entry2 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry2.Text.Add(0, "I also buy rare items.");
        dialog.Entries.Add(entry2);

        // Entry2 → Reply0 (LINK - same target as Entry0)
        var ptrEntry2ToReply0 = new DialogPtr
        {
            Node = reply0,
            Type = DialogNodeType.Reply,
            Index = 0,
            IsLink = true,
            Parent = dialog
        };
        entry2.Pointers.Add(ptrEntry2ToReply0);

        // Entry 3: "Looking for anything specific?"
        var entry3 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry3.Text.Add(0, "Looking for anything specific?");
        dialog.Entries.Add(entry3);

        // Entry3 → Reply0 (LINK - third reference to same target)
        var ptrEntry3ToReply0 = new DialogPtr
        {
            Node = reply0,
            Type = DialogNodeType.Reply,
            Index = 0,
            IsLink = true,
            Parent = dialog
        };
        entry3.Pointers.Add(ptrEntry3ToReply0);

        // Reply 1: "Do you have healing potions?" (Target Y - shared 2 times)
        var reply1 = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString(),
            Speaker = ""
        };
        reply1.Text.Add(0, "Do you have healing potions?");
        dialog.Replies.Add(reply1);

        // Entry1 → Reply1 (ORIGINAL)
        var ptrEntry1ToReply1 = new DialogPtr
        {
            Node = reply1,
            Type = DialogNodeType.Reply,
            Index = 1,
            IsLink = false,
            Parent = dialog
        };
        entry1.Pointers.Add(ptrEntry1ToReply1);

        // Entry 4: "Yes, freshly brewed this morning."
        var entry4 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry4.Text.Add(0, "Yes, freshly brewed this morning.");
        dialog.Entries.Add(entry4);

        // Reply1 → Entry4
        var ptrReply1ToEntry4 = new DialogPtr
        {
            Node = entry4,
            Type = DialogNodeType.Entry,
            Index = 4,
            IsLink = false,
            Parent = dialog
        };
        reply1.Pointers.Add(ptrReply1ToEntry4);

        // Entry2 → Reply1 (LINK - same target as Entry1)
        var ptrEntry2ToReply1 = new DialogPtr
        {
            Node = reply1,
            Type = DialogNodeType.Reply,
            Index = 1,
            IsLink = true,
            Parent = dialog
        };
        entry2.Pointers.Add(ptrEntry2ToReply1);

        // Reply 2: "I'll take a look around."
        var reply2 = new DialogNode
        {
            Type = DialogNodeType.Reply,
            Text = new LocString(),
            Speaker = ""
        };
        reply2.Text.Add(0, "I'll take a look around.");
        dialog.Replies.Add(reply2);

        // Entry3 → Reply2 (ORIGINAL - unique pointer)
        var ptrEntry3ToReply2 = new DialogPtr
        {
            Node = reply2,
            Type = DialogNodeType.Reply,
            Index = 2,
            IsLink = false,
            Parent = dialog
        };
        entry3.Pointers.Add(ptrEntry3ToReply2);

        // Entry 5: "Take your time."
        var entry5 = new DialogNode
        {
            Type = DialogNodeType.Entry,
            Text = new LocString(),
            Speaker = ""
        };
        entry5.Text.Add(0, "Take your time.");
        dialog.Entries.Add(entry5);

        // Reply2 → Entry5
        var ptrReply2ToEntry5 = new DialogPtr
        {
            Node = entry5,
            Type = DialogNodeType.Entry,
            Index = 5,
            IsLink = false,
            Parent = dialog
        };
        reply2.Pointers.Add(ptrReply2ToEntry5);

        // Mark Entry0, Entry2, Entry3 as starting entries (multiple conversation entry points)
        dialog.Starts.Add(new DialogPtr { Node = entry0, Type = DialogNodeType.Entry, Index = 0, IsLink = false, Parent = dialog });
        dialog.Starts.Add(new DialogPtr { Node = entry2, Type = DialogNodeType.Entry, Index = 2, IsLink = false, Parent = dialog });
        dialog.Starts.Add(new DialogPtr { Node = entry3, Type = DialogNodeType.Entry, Index = 3, IsLink = false, Parent = dialog });

        // Rebuild LinkRegistry
        dialog.RebuildLinkRegistry();

        // Save to file
        // Navigate from CreateTest3Dialog/bin/Debug/net9.0 up to Parley root
        // NOTE: Aurora Engine has 12-char filename limit (excluding .dlg extension)
        string outputPath = Path.Combine(
            Environment.CurrentDirectory,
            "..", "..", "..", "..", "..", "TestingTools", "TestFiles", "test3_multi.dlg"
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
        Console.WriteLine("Dialog Structure (complex link pattern):");
        Console.WriteLine();
        Console.WriteLine("Target X (Reply0: \"What are you selling?\") - 3 references:");
        Console.WriteLine("  Entry0 → Reply0 [ORIGINAL]");
        Console.WriteLine("    → Entry1: \"Prices are negotiable.\"");
        Console.WriteLine("      → Reply1: \"Do you have healing potions?\" [ORIGINAL]");
        Console.WriteLine("        → Entry4: \"Yes, freshly brewed...\"");
        Console.WriteLine("  Entry2 → Reply0 [LINK]");
        Console.WriteLine("    → Reply1: \"Do you have healing potions?\" [LINK]");
        Console.WriteLine("  Entry3 → Reply0 [LINK]");
        Console.WriteLine("    → Reply2: \"I'll take a look around.\" [ORIGINAL]");
        Console.WriteLine("      → Entry5: \"Take your time.\"");
        Console.WriteLine();
        Console.WriteLine("Target Y (Reply1: \"Do you have healing potions?\") - 2 references:");
        Console.WriteLine("  Entry1 → Reply1 [ORIGINAL]");
        Console.WriteLine("  Entry2 → Reply1 [LINK]");
        Console.WriteLine();
        Console.WriteLine("Test Instructions:");
        Console.WriteLine("1. Open in Parley, expand all nodes");
        Console.WriteLine("2. Document which pointers are links:");
        Console.WriteLine("   - Entry0 → Reply0: ORIGINAL");
        Console.WriteLine("   - Entry2 → Reply0: LINK");
        Console.WriteLine("   - Entry3 → Reply0: LINK");
        Console.WriteLine("   - Entry1 → Reply1: ORIGINAL");
        Console.WriteLine("   - Entry2 → Reply1: LINK");
        Console.WriteLine("   - Entry3 → Reply2: ORIGINAL");
        Console.WriteLine("3. Delete Entry2 (has both original and link pointers)");
        Console.WriteLine("4. Press Ctrl+Z (Undo)");
        Console.WriteLine("5. Verify all IsLink flags match documentation above");
        Console.WriteLine("6. Press Ctrl+Y (Redo) - delete Entry2 again");
        Console.WriteLine("7. Press Ctrl+Z (Undo) - restore Entry2 again");
        Console.WriteLine("8. Repeat Redo/Undo cycle 3 more times");
        Console.WriteLine("9. VERIFY: Structure always matches documented state");
        Console.WriteLine();
        Console.WriteLine("This tests Issue #28 fix: Complex link preservation through undo/redo cycles.");
    }
}
