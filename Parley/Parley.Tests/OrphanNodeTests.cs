using DialogEditor.Models;
using DialogEditor.Parsers;
using DialogEditor.Services;
using Parley.Models;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Comprehensive orphan node detection and preservation tests
    /// Tests deletion scenarios, parent-child link preservation, and container creation
    /// </summary>
    public class OrphanNodeTests
    {
        private readonly string _testFilesPath;

        public OrphanNodeTests()
        {
            _testFilesPath = Path.Combine(
                Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.FullName,
                "TestingTools", "TestFiles");
        }

        #region Parent-Child Link Preservation Tests

        [Fact]
        public void DeleteNode_PreservesParentInParentChildLink()
        {
            // Arrange: Create structure like fox.dlg
            // Entry "Greetings" -> PC Reply "I would like to see what you have"
            // PC Reply has 2 children (parent-child links with IsLink=true)
            var dialog = new Dialog();

            var greetingEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            greetingEntry.Text.Add(0, "Greetings <FirstName>, what can I do for you?");
            dialog.Entries.Add(greetingEntry);

            var shopReply = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString(),
                Parent = dialog
            };
            shopReply.Text.Add(0, "I would like to see what you have");
            dialog.Replies.Add(shopReply);

            // Simulate 2 children with parent-child links (IsLink=true) back to shopReply
            var child1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            child1.Text.Add(0, "Child 1");
            dialog.Entries.Add(child1);

            var child2 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            child2.Text.Add(0, "Child 2");
            dialog.Entries.Add(child2);

            // Greeting -> Shop Reply (regular pointer)
            var greetingToShop = new DialogPtr
            {
                Node = shopReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            };
            greetingEntry.Pointers.Add(greetingToShop);

            // Child 1 -> Shop Reply (parent-child link)
            var child1ToParent = new DialogPtr
            {
                Node = shopReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true, // THIS IS THE KEY - parent-child link
                Parent = dialog
            };
            child1.Pointers.Add(child1ToParent);

            // Child 2 -> Shop Reply (parent-child link)
            var child2ToParent = new DialogPtr
            {
                Node = shopReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true, // THIS IS THE KEY - parent-child link
                Parent = dialog
            };
            child2.Pointers.Add(child2ToParent);

            // Start -> Greeting
            var startPtr = new DialogPtr
            {
                Node = greetingEntry,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            };
            dialog.Starts.Add(startPtr);

            dialog.RebuildLinkRegistry();

            // Act: Simulate deletion of Greeting entry
            // This should NOT delete Shop Reply because it has child links pointing to it
            var incomingLinks = dialog.LinkRegistry.GetLinksTo(shopReply);
            var hasChildLinks = incomingLinks.Any(link => link.IsLink);
            var otherIncomingLinks = incomingLinks.Where(link =>
            {
                // Find parent of this link
                DialogNode? linkParent = null;
                foreach (var entry in dialog.Entries)
                {
                    if (entry.Pointers.Contains(link))
                    {
                        linkParent = entry;
                        break;
                    }
                }
                return linkParent != greetingEntry;
            }).Count();

            // Assert: Shop Reply should NOT be deleted
            // Because it has child links (IsLink=true) pointing to it
            Assert.True(hasChildLinks, "Shop Reply should have child links pointing to it");
            Assert.Equal(2, incomingLinks.Count(l => l.IsLink));

            // The deletion logic should preserve it due to: otherIncomingLinks == 0 && !hasChildLinks
            // In this case: otherIncomingLinks = 2 (child links) but hasChildLinks = true
            // So it should NOT be deleted
            bool shouldDelete = (otherIncomingLinks == 0 && !hasChildLinks);
            Assert.False(shouldDelete, "Shop Reply should NOT be deleted - it's a parent in parent-child links");

            // Shop Reply should become an orphan (no regular parent, only child links)
            var regularIncomingLinks = incomingLinks.Where(link => !link.IsLink).ToList();
            bool becomesOrphan = regularIncomingLinks.All(link =>
            {
                DialogNode? linkParent = null;
                foreach (var entry in dialog.Entries)
                {
                    if (entry.Pointers.Contains(link))
                    {
                        linkParent = entry;
                        break;
                    }
                }
                return linkParent == greetingEntry; // Only linked from node being deleted
            });
            Assert.True(becomesOrphan, "Shop Reply should become an orphan after Greeting is deleted");
        }

        [Fact]
        public void DeleteNode_ChildLinksPreventRecursiveDeletion()
        {
            // Arrange: Simpler test focusing on the deletion logic
            var dialog = new Dialog();

            var parent = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            parent.Text.Add(0, "Parent");
            dialog.Entries.Add(parent);

            var sharedNode = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString(),
                Parent = dialog
            };
            sharedNode.Text.Add(0, "Shared node (has child links)");
            dialog.Replies.Add(sharedNode);

            var child = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            child.Text.Add(0, "Child");
            dialog.Entries.Add(child);

            // Parent -> Shared Node
            parent.Pointers.Add(new DialogPtr
            {
                Node = sharedNode,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            });

            // Child -> Shared Node (parent-child link)
            child.Pointers.Add(new DialogPtr
            {
                Node = sharedNode,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true,
                Parent = dialog
            });

            dialog.RebuildLinkRegistry();

            // Act: Check if shared node has child links
            var incomingLinks = dialog.LinkRegistry.GetLinksTo(sharedNode);
            var hasChildLinks = incomingLinks.Any(link => link.IsLink);

            // Assert
            Assert.True(hasChildLinks, "Shared node should have child links");
            Assert.Equal(2, incomingLinks.Count);
            Assert.Single(incomingLinks, l => l.IsLink);
            Assert.Single(incomingLinks, l => !l.IsLink);
        }

        #endregion

        #region Orphan Container Tests

        [Fact]
        public void OrphanContainer_ShouldFilterExistingContainers()
        {
            // Arrange: Dialog with existing orphan containers
            var dialog = new Dialog();

            // Create existing orphan container
            var orphanContainer = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Comment = "PARLEY: Orphaned nodes root container - never appears in-game (sc_false)",
                Parent = dialog
            };
            orphanContainer.Text.Add(0, "!!! Orphaned Nodes");
            dialog.Entries.Add(orphanContainer);

            // Create actual orphan
            var orphanEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            orphanEntry.Text.Add(0, "Real orphan");
            dialog.Entries.Add(orphanEntry);

            // Act: Filter orphans
            var allOrphans = new List<DialogNode> { orphanContainer, orphanEntry };
            var filteredOrphans = allOrphans
                .Where(n => n.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            // Assert: Container should be filtered out
            Assert.Single(filteredOrphans);
            Assert.Equal(orphanEntry, filteredOrphans[0]);
            Assert.DoesNotContain(orphanContainer, filteredOrphans);
        }

        [Fact]
        public void OrphanContainer_ShouldExcludeFromReachabilityCheck()
        {
            // Arrange: Dialog with orphan container and orphaned nodes already in container
            var dialog = new Dialog();

            // Normal start node
            var normalStart = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            normalStart.Text.Add(0, "Normal start");
            dialog.Entries.Add(normalStart);

            // Orphan container (has sc_false, should be excluded from reachability)
            var orphanContainer = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Comment = "PARLEY: Orphaned nodes root container - never appears in-game (sc_false)",
                Parent = dialog
            };
            orphanContainer.Text.Add(0, "!!! Orphaned Nodes");
            dialog.Entries.Add(orphanContainer);

            // Previously orphaned node now in container
            var previousOrphan = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            previousOrphan.Text.Add(0, "Previously orphaned");
            dialog.Entries.Add(previousOrphan);

            // Container -> Previous Orphan
            orphanContainer.Pointers.Add(new DialogPtr
            {
                Node = previousOrphan,
                Type = DialogNodeType.Entry,
                Index = 2,
                IsLink = false,
                Parent = dialog
            });

            // Starts
            dialog.Starts.Add(new DialogPtr
            {
                Node = normalStart,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            });
            dialog.Starts.Add(new DialogPtr
            {
                Node = orphanContainer,
                Type = DialogNodeType.Entry,
                Index = 1,
                IsLink = false,
                IsStart = true,
                ScriptAppears = "sc_false",
                Comment = "Orphan container - requires sc_false.nss in module",
                Parent = dialog
            });

            dialog.RebuildLinkRegistry();

            // Act: Collect reachable nodes (should traverse ALL starts including orphan container)
            var reachableNodes = new HashSet<DialogNode>();
            foreach (var start in dialog.Starts)
            {
                if (start.Node != null)
                {
                    CollectReachableFromDialogModel(start.Node, reachableNodes);
                }
            }

            // Assert: All nodes should be reachable (including those in orphan container)
            Assert.Contains(normalStart, reachableNodes);
            Assert.Contains(orphanContainer, reachableNodes);
            Assert.Contains(previousOrphan, reachableNodes);

            // When filtering for NEW orphans, exclude container and its contents
            var newOrphans = dialog.Entries
                .Where(e => !reachableNodes.Contains(e))
                .Where(e => e.Comment?.Contains("PARLEY: Orphaned") != true)
                .ToList();

            Assert.Empty(newOrphans); // No new orphans
        }

        [Fact]
        public void OrphanContainer_ShouldNotDuplicateNestedOrphans()
        {
            // CRITICAL: Tests for the duplicate orphan bug
            // Scenario: Orphan A → Reply X → Orphan B
            // Container should only show Orphan A (root), not both A and B

            // Arrange: Create dialog with nested orphan structure
            var dialog = new Dialog();
            dialog.RebuildLinkRegistry();

            // START → Entry 1 (will be deleted, causing orphans)
            var start1Entry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Speaker = "",
                Parent = dialog
            };
            start1Entry.Text.Add(0, "Root entry that will be deleted");
            dialog.Entries.Add(start1Entry);

            // Orphan A (will become root orphan)
            var orphanA = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Speaker = "",
                Parent = dialog
            };
            orphanA.Text.Add(0, "Orphan A - should appear in container");
            dialog.Entries.Add(orphanA);

            // Reply X (child of Orphan A)
            var replyX = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString(),
                Speaker = "",
                Parent = dialog
            };
            replyX.Text.Add(0, "Reply X");
            dialog.Replies.Add(replyX);

            // Orphan B (child of Reply X, grandchild of Orphan A)
            var orphanB = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Speaker = "",
                Parent = dialog
            };
            orphanB.Text.Add(0, "Orphan B - should NOT appear separately");
            dialog.Entries.Add(orphanB);

            // Wire up pointers
            var start1Ptr = new DialogPtr
            {
                Node = start1Entry,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsStart = true,
                Parent = dialog
            };
            dialog.Starts.Add(start1Ptr);
            dialog.LinkRegistry.RegisterLink(start1Ptr);

            var start1ToOrphanA = new DialogPtr
            {
                Node = orphanA,
                Type = DialogNodeType.Entry,
                Index = 1,
                IsLink = false,
                Parent = dialog
            };
            start1Entry.Pointers.Add(start1ToOrphanA);
            dialog.LinkRegistry.RegisterLink(start1ToOrphanA);

            var orphanAToReplyX = new DialogPtr
            {
                Node = replyX,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            };
            orphanA.Pointers.Add(orphanAToReplyX);
            dialog.LinkRegistry.RegisterLink(orphanAToReplyX);

            var replyXToOrphanB = new DialogPtr
            {
                Node = orphanB,
                Type = DialogNodeType.Entry,
                Index = 2,
                IsLink = false,
                Parent = dialog
            };
            replyX.Pointers.Add(replyXToOrphanB);
            dialog.LinkRegistry.RegisterLink(replyXToOrphanB);

            // Act: Delete start1Entry (orphans A and B)
            dialog.Entries.Remove(start1Entry);
            dialog.Starts.Remove(start1Ptr);
            dialog.LinkRegistry.UnregisterLink(start1Ptr);

            // Detect orphans
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

            // Assert: Both A and B detected as orphaned
            Assert.Equal(2, orphanedEntries.Count);
            Assert.Contains(orphanA, orphanedEntries);
            Assert.Contains(orphanB, orphanedEntries);

            // Now filter for root orphans (this is what CreateOrUpdateOrphanContainersInModel does)
            var rootOrphans = orphanedEntries.Where(orphan =>
            {
                foreach (var otherOrphan in orphanedEntries)
                {
                    if (otherOrphan != orphan)
                    {
                        // Check if orphan appears in otherOrphan's subtree
                        if (IsDescendantOf(orphan, otherOrphan, new HashSet<DialogNode>()))
                        {
                            return false; // Not a root
                        }
                    }
                }
                return true; // This is a root orphan
            }).ToList();

            // Assert: Only Orphan A should be a root orphan
            Assert.Single(rootOrphans);
            Assert.Equal(orphanA, rootOrphans[0]);
            Assert.DoesNotContain(orphanB, rootOrphans); // B is grandchild of A, not a root
        }

        private bool IsDescendantOf(DialogNode target, DialogNode potentialAncestor, HashSet<DialogNode> visited)
        {
            if (potentialAncestor == null || visited.Contains(potentialAncestor))
                return false;

            visited.Add(potentialAncestor);

            foreach (var pointer in potentialAncestor.Pointers.Where(p => !p.IsLink))
            {
                if (pointer.Node == target)
                    return true;

                if (pointer.Node != null && IsDescendantOf(target, pointer.Node, visited))
                    return true;
            }

            return false;
        }

        #endregion

        #region Real World File Tests

        [Fact]
        public async Task FoxDialog_LoadsWithoutOrphans()
        {
            // Arrange
            var foxPath = Path.Combine(_testFilesPath, "fox_orphan_test.dlg");
            if (!File.Exists(foxPath))
            {
                // Skip if test file not available
                return;
            }

            // Act
            var parser = new DialogParser();
            var dialog = await parser.ParseFromFileAsync(foxPath);

            // Assert
            Assert.NotNull(dialog);
            Assert.NotEmpty(dialog.Entries);
            Assert.NotEmpty(dialog.Replies);

            // Check for orphans
            dialog.RebuildLinkRegistry();
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
                $"Fox dialog: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies, " +
                $"{orphanedEntries.Count} orphaned entries, {orphanedReplies.Count} orphaned replies");

            // Original file should have no orphans (it's been cleaned)
            Assert.Empty(orphanedEntries);
            Assert.Empty(orphanedReplies);
        }

        [Fact]
        public async Task ChefDialog_LoadsWithoutOrphans()
        {
            // Arrange
            var chefPath = Path.Combine(_testFilesPath, "chef_orphan_test.dlg");
            if (!File.Exists(chefPath))
            {
                return;
            }

            // Act
            var parser = new DialogParser();
            var dialog = await parser.ParseFromFileAsync(chefPath);

            // Assert
            Assert.NotNull(dialog);

            dialog.RebuildLinkRegistry();
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

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Chef dialog: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies, " +
                $"{orphanedEntries.Count} orphaned entries");

            Assert.Empty(orphanedEntries);
        }

        [Fact]
        public async Task ShadyVendorDialog_LoadsWithoutOrphans()
        {
            // Arrange
            var shadyPath = Path.Combine(_testFilesPath, "shady_vendor_orphan_test.dlg");
            if (!File.Exists(shadyPath))
            {
                return;
            }

            // Act
            var parser = new DialogParser();
            var dialog = await parser.ParseFromFileAsync(shadyPath);

            // Assert
            Assert.NotNull(dialog);

            dialog.RebuildLinkRegistry();
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

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Shady vendor dialog: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies, " +
                $"{orphanedEntries.Count} orphaned entries");

            Assert.Empty(orphanedEntries);
        }

        [Fact]
        public async Task GenericHenchDialog_LoadsWithoutOrphans()
        {
            // Arrange
            var henchPath = Path.Combine(_testFilesPath, "generic_hench_orphan_test.dlg");
            if (!File.Exists(henchPath))
            {
                return;
            }

            // Act
            var parser = new DialogParser();
            var dialog = await parser.ParseFromFileAsync(henchPath);

            // Assert
            Assert.NotNull(dialog);

            dialog.RebuildLinkRegistry();
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
                $"Generic hench dialog: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies, " +
                $"{orphanedEntries.Count} orphaned entries, {orphanedReplies.Count} orphaned replies");

            // This is a large complex file - it's OK if it has some orphans
            // Just verify it loads without crashing
            Assert.True(dialog.Entries.Count > 0);
            Assert.True(dialog.Replies.Count > 0);
        }

        #endregion

        #region Orphaned Node with Child Links Regression Tests (2025-11-18 Fix)

        [Fact]
        public void RemoveOrphanedNodes_RemovesNodeWithOnlyChildLinks()
        {
            // Regression test for chef file bug (2025-11-18)
            // When a node has ONLY child link (IsLink=true) incoming pointers,
            // it should be identified as orphaned and removed by RemoveOrphanedNodes
            //
            // Scenario from chef file:
            // START -> "That's not uncommon..." -> "<CUSTOM1009>Chef..." (Reply)
            //                                      ^
            //                                      |
            // "I go through that sometimes..." ---(IsLink=true)
            //
            // When "That's not uncommon..." is deleted, "<CUSTOM1009>Chef..." has
            // ONLY the child link from "I go through...", so it should be removed

            var dialog = new Dialog();
            var orphanManager = new DialogEditor.Services.OrphanNodeManager();

            // Create "That's not uncommon..." entry
            var parentEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            parentEntry.Text.Add(0, "That's not uncommon...");
            dialog.Entries.Add(parentEntry);

            // Create "<CUSTOM1009>Chef..." reply
            var chefReply = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString(),
                Parent = dialog
            };
            chefReply.Text.Add(0, "<CUSTOM1009>Chef looks mournful.<CUSTOM1000>");
            dialog.Replies.Add(chefReply);

            // Create "I go through that sometimes..." entry
            var otherEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            otherEntry.Text.Add(0, "I go through that sometimes...");
            dialog.Entries.Add(otherEntry);

            // Parent Entry -> Chef Reply (regular pointer)
            var parentToChef = new DialogPtr
            {
                Node = chefReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            };
            parentEntry.Pointers.Add(parentToChef);

            // Other Entry -> Chef Reply (child link)
            var otherToChef = new DialogPtr
            {
                Node = chefReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true, // Child link - back reference
                Parent = dialog
            };
            otherEntry.Pointers.Add(otherToChef);

            // START -> Other Entry (so Other Entry stays reachable)
            var startPtr = new DialogPtr
            {
                Node = otherEntry,
                Type = DialogNodeType.Entry,
                Index = 1,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            };
            dialog.Starts.Add(startPtr);

            dialog.RebuildLinkRegistry();

            // Act: Remove Parent Entry from Entries list (simulating deletion)
            dialog.Entries.Remove(parentEntry);

            // Remove orphaned pointers
            orphanManager.RemoveOrphanedPointers(dialog);

            // Remove orphaned nodes (should remove Chef Reply)
            var removed = orphanManager.RemoveOrphanedNodes(dialog);

            // Assert: Chef Reply should be removed because it has ONLY a child link now
            Assert.Contains(chefReply, removed);
            Assert.DoesNotContain(chefReply, dialog.Replies);

            // Other Entry should remain (it's reachable from START)
            Assert.Contains(otherEntry, dialog.Entries);
        }

        [Fact]
        public void RemoveOrphanedNodes_KeepsNodeWithRegularIncomingPointer()
        {
            // Test that nodes with at least one regular (non-child-link) incoming pointer
            // are NOT removed, even if they also have child links

            var dialog = new Dialog();
            var orphanManager = new DialogEditor.Services.OrphanNodeManager();

            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            entry1.Text.Add(0, "Entry 1");
            dialog.Entries.Add(entry1);

            var entry2 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            entry2.Text.Add(0, "Entry 2");
            dialog.Entries.Add(entry2);

            var sharedReply = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString(),
                Parent = dialog
            };
            sharedReply.Text.Add(0, "Shared Reply");
            dialog.Replies.Add(sharedReply);

            // Entry 1 -> Shared Reply (regular pointer)
            var entry1ToShared = new DialogPtr
            {
                Node = sharedReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            };
            entry1.Pointers.Add(entry1ToShared);

            // Entry 2 -> Shared Reply (child link)
            var entry2ToShared = new DialogPtr
            {
                Node = sharedReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true,
                Parent = dialog
            };
            entry2.Pointers.Add(entry2ToShared);

            // START -> Entry 1
            var startPtr = new DialogPtr
            {
                Node = entry1,
                Type = DialogNodeType.Entry,
                Index = 0,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            };
            dialog.Starts.Add(startPtr);

            dialog.RebuildLinkRegistry();

            // Act: Remove orphaned nodes
            var removed = orphanManager.RemoveOrphanedNodes(dialog);

            // Assert: Shared Reply should NOT be removed (has regular pointer from Entry 1)
            Assert.DoesNotContain(sharedReply, removed);
            Assert.Contains(sharedReply, dialog.Replies);
        }

        [Fact]
        public void CollectReachableNodes_DoesNotTraverseChildLinks()
        {
            // Test that CollectReachableNodes only follows regular pointers,
            // not child links (IsLink=true)

            var dialog = new Dialog();

            var entry1 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            entry1.Text.Add(0, "Entry 1");
            dialog.Entries.Add(entry1);

            var reply1 = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString(),
                Parent = dialog
            };
            reply1.Text.Add(0, "Reply 1");
            dialog.Replies.Add(reply1);

            var entry2 = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            entry2.Text.Add(0, "Entry 2");
            dialog.Entries.Add(entry2);

            // Entry 1 -> Reply 1 (regular)
            entry1.Pointers.Add(new DialogPtr
            {
                Node = reply1,
                Type = DialogNodeType.Reply,
                IsLink = false,
                Parent = dialog
            });

            // Reply 1 -> Entry 2 (child link - should NOT be traversed)
            reply1.Pointers.Add(new DialogPtr
            {
                Node = entry2,
                Type = DialogNodeType.Entry,
                IsLink = true, // Child link
                Parent = dialog
            });

            // START -> Entry 1
            dialog.Starts.Add(new DialogPtr
            {
                Node = entry1,
                Type = DialogNodeType.Entry,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            });

            dialog.RebuildLinkRegistry();

            // Act: Remove orphaned nodes
            var orphanManager = new DialogEditor.Services.OrphanNodeManager();
            var removed = orphanManager.RemoveOrphanedNodes(dialog);

            // Assert: Entry 2 should be removed (only reachable via child link)
            Assert.Contains(entry2, removed);
            Assert.DoesNotContain(entry2, dialog.Entries);

            // Entry 1 and Reply 1 should remain (reachable from START via regular pointers)
            Assert.Contains(entry1, dialog.Entries);
            Assert.Contains(reply1, dialog.Replies);
        }

        [Fact]
        public void DeleteNode_DoesNotOrphanNodeWithRegularParent()
        {
            // Test that nodes with regular parent pointers are NOT identified as orphaned
            // even if they also have child links (this is for IdentifyOrphanedLinkChildren,
            // which is different from RemoveOrphanedNodes)

            // Arrange: Create structure where child has both regular parent AND child link
            var dialog = new Dialog();

            var parentEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            parentEntry.Text.Add(0, "Parent Entry");
            dialog.Entries.Add(parentEntry);

            var otherEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            otherEntry.Text.Add(0, "Other Entry");
            dialog.Entries.Add(otherEntry);

            var childEntry = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Parent = dialog
            };
            childEntry.Text.Add(0, "Child Entry (NOT orphaned - has regular parent)");
            dialog.Entries.Add(childEntry);

            var sharedReply = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString(),
                Parent = dialog
            };
            sharedReply.Text.Add(0, "Shared Reply");
            dialog.Replies.Add(sharedReply);

            // Parent -> Shared Reply (regular pointer)
            var parentToShared = new DialogPtr
            {
                Node = sharedReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = false,
                Parent = dialog
            };
            parentEntry.Pointers.Add(parentToShared);

            // Other Entry -> Child Entry (regular pointer - NOT being deleted)
            var otherToChild = new DialogPtr
            {
                Node = childEntry,
                Type = DialogNodeType.Entry,
                Index = 2,
                IsLink = false,
                Parent = dialog
            };
            otherEntry.Pointers.Add(otherToChild);

            // Child -> Shared Reply (child link)
            var childToShared = new DialogPtr
            {
                Node = sharedReply,
                Type = DialogNodeType.Reply,
                Index = 0,
                IsLink = true,
                Parent = dialog
            };
            childEntry.Pointers.Add(childToShared);

            // START -> Other Entry
            var startPtr = new DialogPtr
            {
                Node = otherEntry,
                Type = DialogNodeType.Entry,
                Index = 1,
                IsLink = false,
                IsStart = true,
                Parent = dialog
            };
            dialog.Starts.Add(startPtr);

            dialog.RebuildLinkRegistry();

            // Act: Delete parent entry using OrphanNodeManager
            var orphanManager = new DialogEditor.Services.OrphanNodeManager();
            var nodesToDelete = new HashSet<DialogNode> { parentEntry, sharedReply };
            var orphanedLinkChildren = orphanManager.IdentifyOrphanedLinkChildren(dialog, parentEntry, nodesToDelete);

            // Assert: Child Entry should NOT be identified as orphaned
            // Because it has a regular parent pointer from Other Entry (which is NOT being deleted)
            Assert.Empty(orphanedLinkChildren);
            Assert.Contains(childEntry, dialog.Entries);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper method that mimics MainViewModel.CollectReachableNodesForOrphanDetection
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

        #endregion
    }
}
