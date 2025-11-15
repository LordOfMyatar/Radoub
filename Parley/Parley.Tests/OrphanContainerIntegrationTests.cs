using DialogEditor.Models;
using DialogEditor.Parsers;
using DialogEditor.Services;
using Parley.Models;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Integration tests for orphan container creation and persistence
    /// Tests that orphan containers are actually written to DLG files
    /// </summary>
    public class OrphanContainerIntegrationTests
    {
        private readonly string _testFilesPath;

        public OrphanContainerIntegrationTests()
        {
            _testFilesPath = Path.Combine(
                Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.FullName,
                "TestingTools", "TestFiles");
        }

        [Fact]
        public async Task DeletingParentEntry_CreatesOrphanContainer_AndPersistsToFile()
        {
            // Arrange: Load a clean dialog file
            var testFilePath = Path.Combine(_testFilesPath, "fox_orphan_test.dlg");
            if (!File.Exists(testFilePath))
            {
                // Skip if test file not available
                return;
            }

            var outputPath = Path.Combine(_testFilesPath, "fox_orphan_deletion_test_output.dlg");

            // Copy to test output path
            File.Copy(testFilePath, outputPath, overwrite: true);

            var parser = new DialogParser();
            var dialog = await parser.ParseFromFileAsync(outputPath);

            Assert.NotNull(dialog);
            dialog.RebuildLinkRegistry();

            // Find an entry with children (simulate the fox.dlg "Greetings" entry scenario)
            var entryWithChildren = dialog.Entries.FirstOrDefault(e => e.Pointers.Any());
            if (entryWithChildren == null)
            {
                // Skip if no suitable entry found
                return;
            }

            // Get children before deletion
            var childrenBeforeDeletion = entryWithChildren.Pointers
                .Select(p => p.Node)
                .Where(n => n != null)
                .ToList();

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Test: Deleting entry '{entryWithChildren.DisplayText}' with {childrenBeforeDeletion.Count} children");

            // Check if any children are parents in parent-child links
            int childrenWithChildLinks = 0;
            foreach (var child in childrenBeforeDeletion)
            {
                if (child != null)
                {
                    var incomingLinks = dialog.LinkRegistry.GetLinksTo(child);
                    if (incomingLinks.Any(link => link.IsLink))
                    {
                        childrenWithChildLinks++;
                        UnifiedLogger.LogApplication(LogLevel.INFO,
                            $"  Child '{child.DisplayText}' has child links (should be preserved)");
                    }
                }
            }

            // Act: Delete the entry
            dialog.Entries.Remove(entryWithChildren);

            // Remove pointers from Starts
            var startsToRemove = dialog.Starts.Where(s => s.Node == entryWithChildren).ToList();
            foreach (var start in startsToRemove)
            {
                dialog.Starts.Remove(start);
                dialog.LinkRegistry.UnregisterLink(start);
            }

            // Note: In real usage, MainViewModel.RecalculatePointerIndices() would be called here
            // For this test, we'll manually update indices if needed
            dialog.RebuildLinkRegistry();

            // Manually trigger orphan detection and container creation
            // (Normally MainViewModel does this in RefreshTreeView -> PopulateDialogNodes)
            var reachableNodes = new HashSet<DialogNode>();
            foreach (var start in dialog.Starts)
            {
                if (start.Node != null)
                {
                    CollectReachableFromDialogModel(start.Node, reachableNodes);
                }
            }

            var orphanedEntries = dialog.Entries
                .Where(e => !reachableNodes.Contains(e))
                .Where(e => e.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            var orphanedReplies = dialog.Replies
                .Where(r => !reachableNodes.Contains(r))
                .Where(r => r.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"After deletion: {orphanedEntries.Count} orphaned entries, {orphanedReplies.Count} orphaned replies");

            // If we have orphans, create the container
            if (orphanedEntries.Count > 0 || orphanedReplies.Count > 0)
            {
                CreateOrphanContainer(dialog, orphanedEntries, orphanedReplies);
            }

            // Save the file
            await parser.WriteToFileAsync(dialog, outputPath);

            // Assert: Reload the file and verify orphan container exists
            var reloadedDialog = await parser.ParseFromFileAsync(outputPath);
            Assert.NotNull(reloadedDialog);

            var orphanContainer = reloadedDialog.Entries
                .FirstOrDefault(e => e.Comment?.Contains("PARLEY: Orphaned nodes root container") == true);

            if (orphanedEntries.Count > 0 || orphanedReplies.Count > 0)
            {
                Assert.NotNull(orphanContainer);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"✓ Orphan container found in saved file: '{orphanContainer?.DisplayText}'");

                // Verify orphan container has sc_false START
                var orphanStart = reloadedDialog.Starts
                    .FirstOrDefault(s => s.Comment?.Contains("Orphan container") == true);
                Assert.NotNull(orphanStart);
                Assert.Equal("sc_false", orphanStart.ScriptAppears);

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"✓ Orphan container START found with sc_false script");
            }

            // Cleanup
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Mimics MainViewModel.CollectReachableNodesForOrphanDetection
        /// ONLY traverses regular pointers (IsLink=false) from START points
        /// IsLink=true pointers are back-references and should NOT prevent orphaning
        /// </summary>
        private void CollectReachableFromDialogModel(DialogNode node, HashSet<DialogNode> reachableNodes)
        {
            if (node == null || reachableNodes.Contains(node))
                return;

            reachableNodes.Add(node);

            // ONLY traverse regular pointers (IsLink=false) for orphan detection
            // IsLink=true pointers are back-references from link children to their shared parent
            // If we traverse IsLink pointers, link parents appear reachable even when their
            // owning START is deleted, preventing proper orphan detection
            foreach (var pointer in node.Pointers.Where(p => !p.IsLink))
            {
                if (pointer.Node != null)
                {
                    CollectReachableFromDialogModel(pointer.Node, reachableNodes);
                }
            }
        }

        /// <summary>
        /// Simplified version of MainViewModel.CreateOrUpdateOrphanContainers
        /// </summary>
        private void CreateOrphanContainer(Dialog dialog, List<DialogNode> orphanedEntries, List<DialogNode> orphanedReplies)
        {
            // Create root container
            var rootContainer = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Comment = "PARLEY: Orphaned nodes root container - never appears in-game (sc_false)",
                Speaker = "",
                Parent = dialog
            };
            rootContainer.Text.Add(0, "!!! Orphaned Nodes");
            dialog.Entries.Add(rootContainer);

            // Create START pointer with sc_false
            var rootIndex = (uint)(dialog.Entries.Count - 1);
            var rootStart = new DialogPtr
            {
                Node = rootContainer,
                Type = DialogNodeType.Entry,
                Index = rootIndex,
                IsLink = false,
                IsStart = true,
                ScriptAppears = "sc_false",
                ConditionParams = new Dictionary<string, string>(),
                Comment = "Orphan container - requires sc_false.nss in module",
                Parent = dialog
            };
            dialog.Starts.Add(rootStart);
            dialog.LinkRegistry.RegisterLink(rootStart);

            // Add orphaned entries
            if (orphanedEntries.Count > 0)
            {
                var npcCategoryReply = new DialogNode
                {
                    Type = DialogNodeType.Reply,
                    Text = new LocString(),
                    Comment = "PARLEY: Orphaned NPC entries category",
                    Speaker = "",
                    Parent = dialog
                };
                npcCategoryReply.Text.Add(0, "!!! Orphaned NPC Nodes");
                dialog.Replies.Add(npcCategoryReply);

                foreach (var orphan in orphanedEntries)
                {
                    var orphanIndex = (uint)dialog.Entries.IndexOf(orphan);
                    var ptr = new DialogPtr
                    {
                        Node = orphan,
                        Type = DialogNodeType.Entry,
                        Index = orphanIndex,
                        IsLink = false,
                        Parent = dialog,
                        Comment = "Pointer to orphaned NPC entry",
                        ScriptAppears = "",
                        ConditionParams = new Dictionary<string, string>()
                    };
                    npcCategoryReply.Pointers.Add(ptr);
                    dialog.LinkRegistry.RegisterLink(ptr);
                }

                var npcCategoryIndex = (uint)(dialog.Replies.Count - 1);
                var npcCategoryPtr = new DialogPtr
                {
                    Node = npcCategoryReply,
                    Type = DialogNodeType.Reply,
                    Index = npcCategoryIndex,
                    IsLink = false,
                    Parent = dialog
                };
                rootContainer.Pointers.Add(npcCategoryPtr);
                dialog.LinkRegistry.RegisterLink(npcCategoryPtr);
            }

            // Add orphaned replies
            if (orphanedReplies.Count > 0)
            {
                var pcCategoryReply = new DialogNode
                {
                    Type = DialogNodeType.Reply,
                    Text = new LocString(),
                    Comment = "PARLEY: Orphaned PC replies category label",
                    Speaker = "",
                    Parent = dialog
                };
                pcCategoryReply.Text.Add(0, "!!! Orphaned PC Nodes");
                dialog.Replies.Add(pcCategoryReply);

                var pcContainerEntry = new DialogNode
                {
                    Type = DialogNodeType.Entry,
                    Text = new LocString(),
                    Comment = "PARLEY: Orphaned PC replies container entry",
                    Speaker = "",
                    Parent = dialog
                };
                pcContainerEntry.Text.Add(0, "[CONTINUE]");
                dialog.Entries.Add(pcContainerEntry);

                foreach (var orphan in orphanedReplies)
                {
                    var orphanIndex = (uint)dialog.Replies.IndexOf(orphan);
                    var ptr = new DialogPtr
                    {
                        Node = orphan,
                        Type = DialogNodeType.Reply,
                        Index = orphanIndex,
                        IsLink = false,
                        Parent = dialog,
                        Comment = "Pointer to orphaned PC reply",
                        ScriptAppears = "",
                        ConditionParams = new Dictionary<string, string>()
                    };
                    pcContainerEntry.Pointers.Add(ptr);
                    dialog.LinkRegistry.RegisterLink(ptr);
                }

                var containerEntryIndex = (uint)(dialog.Entries.Count - 1);
                var containerEntryPtr = new DialogPtr
                {
                    Node = pcContainerEntry,
                    Type = DialogNodeType.Entry,
                    Index = containerEntryIndex,
                    IsLink = false,
                    Parent = dialog
                };
                pcCategoryReply.Pointers.Add(containerEntryPtr);
                dialog.LinkRegistry.RegisterLink(containerEntryPtr);

                var pcCategoryIndex = (uint)(dialog.Replies.Count - 1);
                var pcCategoryPtr = new DialogPtr
                {
                    Node = pcCategoryReply,
                    Type = DialogNodeType.Reply,
                    Index = pcCategoryIndex,
                    IsLink = false,
                    Parent = dialog
                };
                rootContainer.Pointers.Add(pcCategoryPtr);
                dialog.LinkRegistry.RegisterLink(pcCategoryPtr);
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Created orphan container with {orphanedEntries.Count} entries, {orphanedReplies.Count} replies");
        }

        #endregion
    }
}
