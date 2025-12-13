using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests
{
    /// <summary>
    /// Generates test DLG files for stress testing.
    /// Run these tests to create test files - they output to TestingTools/TestFiles/
    /// </summary>
    public class DeepDialogGenerator
    {
        private readonly ITestOutputHelper _output;
        private readonly DialogFileService _fileService;
        private readonly string _outputPath;

        public DeepDialogGenerator(ITestOutputHelper output)
        {
            _output = output;
            _fileService = new DialogFileService();
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "TestingTools", "TestFiles");
        }

        /// <summary>
        /// Generates a depth-100 dialog with two external entry nodes linking into the chain.
        ///
        /// Structure:
        /// START 1 -> Entry0 -> Reply0 -> Entry1 -> Reply1 -> ... -> Entry99 -> Reply99
        /// START 2 -> Entry100 (links to Reply25)
        /// START 3 -> Entry101 (links to Reply75)
        ///
        /// This tests cascade delete with shared nodes at different depths.
        /// </summary>
        [Fact]
        public async Task Generate_DeepTreeWithCrossReferences()
        {
            var dialog = new Dialog();
            const int chainDepth = 100;

            _output.WriteLine($"Generating deep dialog with depth {chainDepth} and 2 cross-reference entries...");

            // Create the main chain: Entry0 -> Reply0 -> Entry1 -> Reply1 -> ...
            DialogNode? previousNode = null;
            DialogNode? reply25 = null;  // Will link Entry100 here
            DialogNode? reply75 = null;  // Will link Entry101 here

            for (int i = 0; i < chainDepth; i++)
            {
                // Create Entry
                var entry = dialog.CreateNode(DialogNodeType.Entry);
                entry!.Text.Add(0, $"NPC Line {i}: This is entry node at depth {i}");
                dialog.AddNodeInternal(entry, entry.Type);

                if (i == 0)
                {
                    // First entry is a START node
                    var start = dialog.CreatePtr();
                    start!.Node = entry;
                    start.Type = DialogNodeType.Entry;
                    start.Index = 0;
                    start.IsStart = true;
                    start.Parent = dialog;
                    dialog.Starts.Add(start);
                    dialog.LinkRegistry.RegisterLink(start);
                }
                else if (previousNode != null)
                {
                    // Link from previous reply to this entry
                    var ptr = dialog.CreatePtr();
                    ptr!.Node = entry;
                    ptr.Type = DialogNodeType.Entry;
                    ptr.Index = (uint)dialog.Entries.IndexOf(entry);
                    ptr.IsLink = false;
                    ptr.Parent = dialog;
                    previousNode.Pointers.Add(ptr);
                    dialog.LinkRegistry.RegisterLink(ptr);
                }

                // Create Reply
                var reply = dialog.CreateNode(DialogNodeType.Reply);
                reply!.Text.Add(0, $"Player Choice {i}: Response at depth {i}");
                dialog.AddNodeInternal(reply, reply.Type);

                // Link entry to reply
                var entryToReply = dialog.CreatePtr();
                entryToReply!.Node = reply;
                entryToReply.Type = DialogNodeType.Reply;
                entryToReply.Index = (uint)dialog.Replies.IndexOf(reply);
                entryToReply.IsLink = false;
                entryToReply.Parent = dialog;
                entry.Pointers.Add(entryToReply);
                dialog.LinkRegistry.RegisterLink(entryToReply);

                // Save references to specific replies for cross-linking
                if (i == 25) reply25 = reply;
                if (i == 75) reply75 = reply;

                previousNode = reply;
            }

            // Create Entry100 - external entry that links into Reply25 (depth 25)
            var entry100 = dialog.CreateNode(DialogNodeType.Entry);
            entry100!.Text.Add(0, "EXTERNAL ENTRY 100: Links to depth 25 (Reply25)");
            dialog.AddNodeInternal(entry100, entry100.Type);

            // Add as START node
            var start100 = dialog.CreatePtr();
            start100!.Node = entry100;
            start100.Type = DialogNodeType.Entry;
            start100.Index = (uint)dialog.Entries.IndexOf(entry100);
            start100.IsStart = true;
            start100.Parent = dialog;
            dialog.Starts.Add(start100);
            dialog.LinkRegistry.RegisterLink(start100);

            // Link Entry100 -> Reply25 (as a LINK, not original)
            var link25 = dialog.CreatePtr();
            link25!.Node = reply25;
            link25.Type = DialogNodeType.Reply;
            link25.Index = (uint)dialog.Replies.IndexOf(reply25!);
            link25.IsLink = true;  // This is a link reference
            link25.Parent = dialog;
            entry100.Pointers.Add(link25);
            dialog.LinkRegistry.RegisterLink(link25);

            // Create Entry101 - external entry that links into Reply75 (depth 75)
            var entry101 = dialog.CreateNode(DialogNodeType.Entry);
            entry101!.Text.Add(0, "EXTERNAL ENTRY 101: Links to depth 75 (Reply75)");
            dialog.AddNodeInternal(entry101, entry101.Type);

            // Add as START node
            var start101 = dialog.CreatePtr();
            start101!.Node = entry101;
            start101.Type = DialogNodeType.Entry;
            start101.Index = (uint)dialog.Entries.IndexOf(entry101);
            start101.IsStart = true;
            start101.Parent = dialog;
            dialog.Starts.Add(start101);
            dialog.LinkRegistry.RegisterLink(start101);

            // Link Entry101 -> Reply75 (as a LINK, not original)
            var link75 = dialog.CreatePtr();
            link75!.Node = reply75;
            link75.Type = DialogNodeType.Reply;
            link75.Index = (uint)dialog.Replies.IndexOf(reply75!);
            link75.IsLink = true;  // This is a link reference
            link75.Parent = dialog;
            entry101.Pointers.Add(link75);
            dialog.LinkRegistry.RegisterLink(link75);

            // Summary
            _output.WriteLine($"Created dialog:");
            _output.WriteLine($"  - Entries: {dialog.Entries.Count}");
            _output.WriteLine($"  - Replies: {dialog.Replies.Count}");
            _output.WriteLine($"  - Starts: {dialog.Starts.Count}");
            _output.WriteLine($"  - Main chain: Entry0-99 (depth 100)");
            _output.WriteLine($"  - Entry100 links to Reply25 (depth 25)");
            _output.WriteLine($"  - Entry101 links to Reply75 (depth 75)");

            // Save to file (16 char limit: "deep100_xref" = 12 chars)
            var filePath = Path.Combine(_outputPath, "deep100_xref.dlg");

            _output.WriteLine($"\nSaving to: {filePath}");

            bool success = await _fileService.SaveToFileAsync(dialog, filePath);

            Assert.True(success, "Failed to save dialog file");
            Assert.True(File.Exists(filePath), "File was not created");

            var fileInfo = new FileInfo(filePath);
            _output.WriteLine($"File size: {fileInfo.Length} bytes");
            _output.WriteLine($"\n✅ Successfully created {Path.GetFileName(filePath)}");

            // Verify by loading it back
            var loaded = await _fileService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);
            Assert.Equal(dialog.Entries.Count, loaded.Entries.Count);
            Assert.Equal(dialog.Replies.Count, loaded.Replies.Count);
            Assert.Equal(dialog.Starts.Count, loaded.Starts.Count);

            _output.WriteLine($"✅ Round-trip verification passed");
        }

        /// <summary>
        /// Verifies the chain structure of deep100_xref.dlg after loading.
        /// Walks the pointer chain and reports where it breaks (if anywhere).
        /// </summary>
        [Fact]
        public async Task Verify_DeepTreeChainIntegrity()
        {
            var filePath = Path.Combine(_outputPath, "deep100_xref.dlg");
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"File not found: {filePath}");
                _output.WriteLine("Run Generate_DeepTreeWithCrossReferences first");
                return;
            }

            var dialog = await _fileService.LoadFromFileAsync(filePath);
            Assert.NotNull(dialog);

            _output.WriteLine($"Loaded: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies, {dialog.Starts.Count} starts");

            // Find the main chain start (first START entry)
            var mainStart = dialog.Starts.FirstOrDefault(s => !s.IsLink);
            Assert.NotNull(mainStart);
            Assert.NotNull(mainStart.Node);

            _output.WriteLine($"\nWalking main chain from: '{mainStart.Node.Text?.GetDefault()}'");

            // Walk the chain: Entry -> Reply -> Entry -> Reply ...
            var currentNode = mainStart.Node;
            int depth = 0;
            int maxExpectedDepth = 100;

            while (currentNode != null && depth < maxExpectedDepth * 2)
            {
                bool isEntry = currentNode.Type == DialogNodeType.Entry;
                var text = currentNode.Text?.GetDefault() ?? "(no text)";

                // Check for pointers
                if (currentNode.Pointers.Count == 0)
                {
                    _output.WriteLine($"  Depth {depth}: {(isEntry ? "Entry" : "Reply")} - '{text}' - NO POINTERS (end of chain)");
                    break;
                }

                // Find non-link pointer to continue chain
                var nextPtr = currentNode.Pointers.FirstOrDefault(p => !p.IsLink && p.Node != null);

                if (nextPtr == null)
                {
                    _output.WriteLine($"  Depth {depth}: {(isEntry ? "Entry" : "Reply")} - '{text}' - Only link pointers (chain ends here)");
                    // Show what link pointers exist
                    foreach (var p in currentNode.Pointers.Where(p => p.IsLink))
                    {
                        _output.WriteLine($"    -> Link to: '{p.Node?.Text?.GetDefault()}'");
                    }
                    break;
                }

                if (depth < 5 || depth > 95 || depth % 20 == 0)
                {
                    _output.WriteLine($"  Depth {depth}: {(isEntry ? "Entry" : "Reply")} - '{text.Substring(0, Math.Min(40, text.Length))}...'");
                }

                currentNode = nextPtr.Node;
                depth++;
            }

            _output.WriteLine($"\nChain walked to depth: {depth}");
            _output.WriteLine($"Expected depth: ~200 (100 entries + 100 replies)");

            // Should reach depth ~199 (Entry0 -> Reply0 -> Entry1 -> ... -> Reply99)
            Assert.True(depth >= 190, $"Chain broken early at depth {depth}");

            // Also verify the cross-reference entries
            _output.WriteLine("\nVerifying cross-reference entries:");
            foreach (var start in dialog.Starts.Skip(1)) // Skip main chain
            {
                if (start.Node != null)
                {
                    var text = start.Node.Text?.GetDefault() ?? "(no text)";
                    _output.WriteLine($"  START: '{text}'");
                    foreach (var ptr in start.Node.Pointers)
                    {
                        var targetText = ptr.Node?.Text?.GetDefault() ?? "(null)";
                        _output.WriteLine($"    -> {(ptr.IsLink ? "LINK" : "ORIG")} to: '{targetText}'");
                    }
                }
            }
        }

        /// <summary>
        /// Generates a simpler depth-20 version for quick testing.
        /// </summary>
        [Fact]
        public async Task Generate_MediumTreeWithCrossReferences()
        {
            var dialog = new Dialog();
            const int chainDepth = 20;

            _output.WriteLine($"Generating medium dialog with depth {chainDepth} and 2 cross-reference entries...");

            DialogNode? previousNode = null;
            DialogNode? reply5 = null;   // Will link Entry20 here
            DialogNode? reply15 = null;  // Will link Entry21 here

            for (int i = 0; i < chainDepth; i++)
            {
                var entry = dialog.CreateNode(DialogNodeType.Entry);
                entry!.Text.Add(0, $"NPC {i}");
                dialog.AddNodeInternal(entry, entry.Type);

                if (i == 0)
                {
                    var start = dialog.CreatePtr();
                    start!.Node = entry;
                    start.Type = DialogNodeType.Entry;
                    start.Index = 0;
                    start.IsStart = true;
                    start.Parent = dialog;
                    dialog.Starts.Add(start);
                    dialog.LinkRegistry.RegisterLink(start);
                }
                else if (previousNode != null)
                {
                    var ptr = dialog.CreatePtr();
                    ptr!.Node = entry;
                    ptr.Type = DialogNodeType.Entry;
                    ptr.Index = (uint)dialog.Entries.IndexOf(entry);
                    ptr.IsLink = false;
                    ptr.Parent = dialog;
                    previousNode.Pointers.Add(ptr);
                    dialog.LinkRegistry.RegisterLink(ptr);
                }

                var reply = dialog.CreateNode(DialogNodeType.Reply);
                reply!.Text.Add(0, $"PC {i}");
                dialog.AddNodeInternal(reply, reply.Type);

                var entryToReply = dialog.CreatePtr();
                entryToReply!.Node = reply;
                entryToReply.Type = DialogNodeType.Reply;
                entryToReply.Index = (uint)dialog.Replies.IndexOf(reply);
                entryToReply.IsLink = false;
                entryToReply.Parent = dialog;
                entry.Pointers.Add(entryToReply);
                dialog.LinkRegistry.RegisterLink(entryToReply);

                if (i == 5) reply5 = reply;
                if (i == 15) reply15 = reply;

                previousNode = reply;
            }

            // Entry20 links to Reply5
            var entry20 = dialog.CreateNode(DialogNodeType.Entry);
            entry20!.Text.Add(0, "XREF to depth 5");
            dialog.AddNodeInternal(entry20, entry20.Type);

            var start20 = dialog.CreatePtr();
            start20!.Node = entry20;
            start20.Type = DialogNodeType.Entry;
            start20.Index = (uint)dialog.Entries.IndexOf(entry20);
            start20.IsStart = true;
            start20.Parent = dialog;
            dialog.Starts.Add(start20);
            dialog.LinkRegistry.RegisterLink(start20);

            var link5 = dialog.CreatePtr();
            link5!.Node = reply5;
            link5.Type = DialogNodeType.Reply;
            link5.Index = (uint)dialog.Replies.IndexOf(reply5!);
            link5.IsLink = true;
            link5.Parent = dialog;
            entry20.Pointers.Add(link5);
            dialog.LinkRegistry.RegisterLink(link5);

            // Entry21 links to Reply15
            var entry21 = dialog.CreateNode(DialogNodeType.Entry);
            entry21!.Text.Add(0, "XREF to depth 15");
            dialog.AddNodeInternal(entry21, entry21.Type);

            var start21 = dialog.CreatePtr();
            start21!.Node = entry21;
            start21.Type = DialogNodeType.Entry;
            start21.Index = (uint)dialog.Entries.IndexOf(entry21);
            start21.IsStart = true;
            start21.Parent = dialog;
            dialog.Starts.Add(start21);
            dialog.LinkRegistry.RegisterLink(start21);

            var link15 = dialog.CreatePtr();
            link15!.Node = reply15;
            link15.Type = DialogNodeType.Reply;
            link15.Index = (uint)dialog.Replies.IndexOf(reply15!);
            link15.IsLink = true;
            link15.Parent = dialog;
            entry21.Pointers.Add(link15);
            dialog.LinkRegistry.RegisterLink(link15);

            _output.WriteLine($"Created: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies, {dialog.Starts.Count} starts");

            var filePath = Path.Combine(_outputPath, "deep20_xref.dlg");
            bool success = await _fileService.SaveToFileAsync(dialog, filePath);

            Assert.True(success);
            _output.WriteLine($"✅ Created {Path.GetFileName(filePath)}");

            // Verify
            var loaded = await _fileService.LoadFromFileAsync(filePath);
            Assert.NotNull(loaded);
            Assert.Equal(dialog.Entries.Count, loaded.Entries.Count);
            _output.WriteLine($"✅ Round-trip verified");
        }
    }
}