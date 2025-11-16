# Orphan Detection and Scrap System

**Developer Documentation**
**Last Updated**: 2025-11-15
**Related Issues**: #82, #111

## Table of Contents

- [Overview](#overview)
- [What are Orphans?](#what-are-orphans)
- [The Scrap System](#the-scrap-system)
- [Deletion Process](#deletion-process)
- [Orphan Detection](#orphan-detection)
- [Link Preservation](#link-preservation)
- [Scrap Manager Architecture](#scrap-manager-architecture)
- [Restoration Process](#restoration-process)

## Overview

The Orphan Detection and Scrap System prevents accidental data loss in complex conversations. When users delete nodes, the entire subtree is automatically saved to the Scrap Tab (`~/Parley/scrap.json`) for potential restoration, making it safe to explore deletions without permanent consequences.

**Why this approach?**
Complex dialog trees often have non-obvious dependencies. The scrap system gives users a safety net - deleted content can be reviewed and restored if needed, preventing frustration from accidental deletions.

### Ctrl+Z vs Scrap Restoration

**Undo (Ctrl+Z)**:
- Restores entire delete operation
- All nodes deleted together are restored at once
- Maintains exact original structure and hierarchy
- Only works for most recent operations

**Scrap Tab**:
- Allows selective restoration of individual nodes
- Restore one node at a time from any past deletion
- Choose specific nodes from complex deletions
- Persistent across application restarts
- Useful when you want to keep some deletions but recover specific content

**Key Components:**
- `ScrapManager` - Manages scrap storage in user preferences
- `MainViewModel.DeleteNode()` - Handles deletion with orphan detection
- `MainViewModel.RemoveOrphanedPointers()` - Cleans up broken pointers
- `LinkRegistry` - Tracks all pointers for orphan detection

## What are Orphans?

**Orphaned Nodes** are nodes that become unreachable after a deletion operation:

### Example 1: Simple Orphan

```
Before Deletion:
┌─────────────────────┐
│ Entry A (START)     │
│   └─→ Reply B       │
│         └─→ Entry C │  ← This becomes orphaned
└─────────────────────┘

User deletes Reply B

After Deletion:
┌─────────────────────┐
│ Entry A (START)     │  ← No children
└─────────────────────┘

Entry C is still in dialog.Entries list but has no incoming pointers
→ ORPHANED
```

### Example 2: Shared Node (NOT Orphaned)

```
Before Deletion:
┌─────────────────────┐
│ Entry A (START)     │
│   ├─→ Reply B       │
│   │     └─→ Entry C │  ← Parent node (original pointer)
│   └─→ Reply D       │
│         └─→ Entry C │  ← Child link (IsLink=true, points to parent)
└─────────────────────┘

User deletes Reply B (and Entry C)

After Deletion:
┌─────────────────────┐
│ Entry A (START)     │
│   └─→ Reply D       │
│         └─→ Entry C │  ← Still reachable (child link preserved)
└─────────────────────┘

Entry C is NOT orphaned (child link from Reply D keeps it reachable)
```

### Example 3: Link Preservation

```
Before Deletion:
┌─────────────────────┐
│ Entry A (START)     │
│   └─→ Reply B       │
│         └─→ Entry C │  ← Parent node
│               └─→ Reply D (IsLink=true)  ← Child link
└─────────────────────┘

User deletes Reply B and Entry C

After Deletion:
┌─────────────────────┐
│ Entry A (START)     │
└─────────────────────┘

Reply D is PRESERVED (not deleted)
→ Has IsLink=true pointer, indicating it's a child in parent-child relationship
→ Becomes orphaned but stays in dialog.Replies list for restoration
```

## The Scrap System

### Design Philosophy

The scrap system stores deleted content in user preferences (`~/Parley/scrap.json`) rather than in the `.dlg` file itself.

**Benefits:**
1. **Aurora-Compatible** - .dlg files remain clean (no special container nodes)
2. **Per-File Scrap** - Each dialog has its own scrap entries
3. **Persistent** - Scrap survives app restarts
4. **Safety Net** - Users can explore deletions without fear
5. **Metadata** - Stores hierarchy info (nesting level, parent node)

### Scrap Storage Format

**File Location:** `~/Parley/scrap.json`

**Structure:**
```json
{
  "entries": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "filePath": "~/my_dialog.dlg",
      "timestamp": "2025-11-15T10:30:00Z",
      "operation": "deleted",
      "nodeType": "Entry",
      "nodeText": "[Owner] \"Hello there!\"",
      "speaker": "Owner",
      "originalIndex": 3,
      "nestingLevel": 2,
      "parentNodeText": "[Owner] \"Greetings!\"",
      "serializedNode": "{...full DialogNode JSON...}"
    }
  ]
}
```

**Fields:**
- `id` - Unique GUID for restoration
- `filePath` - File path (sanitized with ~ for home directory)
- `timestamp` - When node was deleted
- `operation` - "deleted", "cut", etc.
- `nodeType` - "Entry" or "Reply"
- `nodeText` - Preview text for topmost parent of deleted subtree
- `speaker` - Speaker tag
- `originalIndex` - Index in Entries/Replies list
- `nestingLevel` - Depth in tree (0=direct child of ROOT)
- `parentNodeText` - Parent node preview (for context)
- `serializedNode` - Full node JSON (includes all properties and children)

## Deletion Process

### Step-by-Step Flow

```
User deletes Node X
  ↓
MainViewModel.DeleteNode(X)
  ├─→ 1. Check for incoming links (warn user)
  │     └─→ LinkRegistry.GetLinksTo(X)
  │
  ├─→ 2. Collect nodes to delete (X + children)
  │     └─→ CollectNodeAndChildren(X, nodesToDelete, hierarchyInfo)
  │
  ├─→ 3. Add to scrap BEFORE deleting
  │     └─→ ScrapManager.AddToScrap(fileName, nodesToDelete, "deleted", hierarchyInfo)
  │
  ├─→ 4. Recursively delete node tree
  │     └─→ DeleteNodeRecursive(X)
  │         ├─→ For each child:
  │         │     ├─→ Check if shared (multiple incoming links)
  │         │     ├─→ Check if parent in parent-child link (IsLink=true)
  │         │     ├─→ If NOT shared and NOT parent → DeleteNodeRecursive(child)
  │         │     └─→ If shared or parent → PRESERVE (becomes orphaned)
  │         ├─→ Unregister all pointers from LinkRegistry
  │         ├─→ Remove from parent.Pointers or dialog.Starts
  │         └─→ Remove from dialog.Entries or dialog.Replies
  │
  ├─→ 5. Recalculate pointer indices
  │     └─→ RecalculatePointerIndices()
  │
  ├─→ 6. Remove orphaned pointers
  │     └─→ RemoveOrphanedPointers()
  │         ├─→ Clean dialog.Starts (remove pointers to deleted nodes)
  │         ├─→ Clean Entry.Pointers (remove pointers to deleted nodes)
  │         └─→ Clean Reply.Pointers (remove pointers to deleted nodes)
  │
  └─→ 7. Refresh tree view
        └─→ RefreshTreeView()
```

### CollectNodeAndChildren()

Recursively collects node and descendants for scrap storage:

```csharp
private void CollectNodeAndChildren(DialogNode node, List<DialogNode> collected,
    Dictionary<DialogNode, (int level, DialogNode? parent)>? hierarchyInfo = null,
    int level = 0, DialogNode? parent = null)
{
    collected.Add(node);

    // Store hierarchy metadata for scrap display
    if (hierarchyInfo != null)
    {
        hierarchyInfo[node] = (level, parent);
    }

    // Recurse into children (skip links - they're not owned by this node)
    foreach (var ptr in node.Pointers)
    {
        if (ptr.Node != null && !ptr.IsLink && !collected.Contains(ptr.Node))
        {
            CollectNodeAndChildren(ptr.Node, collected, hierarchyInfo, level + 1, node);
        }
    }
}
```

**Key Points:**
- Links (`IsLink=true`) are NOT collected - they're not owned by this node
- Hierarchy info tracks nesting level and parent for scrap display
- Prevents duplicate collection with `!collected.Contains()` check

## Orphan Detection

### DeleteNodeRecursive()

Intelligently deletes nodes while preserving shared content:

```csharp
private void DeleteNodeRecursive(DialogNode node)
{
    // Process children first
    if (node.Pointers != null && node.Pointers.Count > 0)
    {
        var pointersToDelete = node.Pointers.ToList();  // Copy to avoid modification during iteration

        foreach (var ptr in pointersToDelete)
        {
            if (ptr.Node != null)
            {
                // Get ALL incoming links to child
                var incomingLinks = CurrentDialog?.LinkRegistry.GetLinksTo(ptr.Node) ?? new List<DialogPtr>();

                // Count links from OTHER parents (not this node)
                var otherIncomingLinks = incomingLinks.Where(link =>
                {
                    DialogNode? linkParent = FindParentNode(link);
                    return linkParent != node;  // Exclude links from current node
                }).Count();

                // Check if child is a parent in parent-child link
                var hasChildLinks = incomingLinks.Any(link => link.IsLink);

                if (otherIncomingLinks == 0 && !hasChildLinks)
                {
                    // No other references - safe to delete
                    DeleteNodeRecursive(ptr.Node);
                }
                else
                {
                    // PRESERVE - either shared or parent in parent-child link
                    var reason = hasChildLinks ?
                        $"is parent in parent-child link(s)" :
                        $"has {otherIncomingLinks} other references";

                    LogInfo($"PRESERVING node (will become orphaned): '{ptr.Node.DisplayText}' ({reason})");
                    // Node stays in dialog.Entries/Replies but becomes orphaned
                }
            }
        }

        // Unregister and clear pointers
        foreach (var ptr in pointersToDelete)
        {
            CurrentDialog?.LinkRegistry.UnregisterLink(ptr);
        }
        node.Pointers.Clear();
    }

    // Remove incoming pointers to this node
    var incomingPointers = CurrentDialog?.LinkRegistry.GetLinksTo(node).ToList() ?? new List<DialogPtr>();

    foreach (var incomingPtr in incomingPointers)
    {
        CurrentDialog?.LinkRegistry.UnregisterLink(incomingPtr);

        // Remove from Starts
        if (CurrentDialog?.Starts.Contains(incomingPtr) ?? false)
        {
            CurrentDialog?.Starts.Remove(incomingPtr);
        }

        // Remove from parent nodes
        RemovePointerFromParent(incomingPtr);
    }

    // Remove from dialog lists
    if (node.Type == DialogNodeType.Entry)
    {
        CurrentDialog?.Entries.Remove(node);
    }
    else
    {
        CurrentDialog?.Replies.Remove(node);
    }
}
```

**Preservation Rules:**

1. **Shared Nodes** - Multiple incoming links → PRESERVE
   ```
   Entry A → Reply B → Entry C ← Reply D
                      ↑
                 Shared node

   Delete Reply B → Entry C preserved (still linked from Reply D)
   ```

2. **Parent-Child Links** - `IsLink=true` pointers → PRESERVE child
   ```
   Entry A → Reply B (IsLink=true points to Entry C)

   Entry C is a parent node in parent-child relationship
   Delete Entry A → Entry C preserved (parent in link)
   ```

3. **Unshared Non-Parents** - No other links, not a parent → DELETE
   ```
   Entry A → Reply B → Entry C (no other links)

   Delete Entry A → Reply B and Entry C both deleted
   ```

## Link Preservation

### Parent-Child Link Detection

Parent-child links (`IsLink=true`) create special preservation rules:

```csharp
// Check if this node is a parent in parent-child link(s)
var hasChildLinks = incomingLinks.Any(link => link.IsLink);

if (hasChildLinks)
{
    // PRESERVE - this node is a parent in parent-child relationship
    // Even if no other incoming links exist
    LogInfo($"PRESERVING node (parent in {childLinkCount} parent-child links)");
}
```

**Why Preserve Parents?**

Parent-child links represent "this node exists elsewhere, here's a shortcut to it". The parent node must remain intact even if the child link is deleted.

**Example:**
```
Conversation 1:
  Entry A → Reply B → Entry C (original)

Conversation 2:
  Entry D → Reply E (IsLink=true → Entry C)  ← Child link

If user deletes Entry D:
  - Reply E is deleted
  - Entry C is PRESERVED (it's the parent in the link)
  - Entry C becomes orphaned (no incoming links from this conversation)
  - Entry C still exists for restoration or use in other conversations
```

## Scrap Manager Architecture

### Service Responsibilities

```csharp
public class ScrapManager
{
    private readonly string _scrapFilePath;           // ~/Parley/scrap.json
    private ScrapData _scrapData;                     // In-memory scrap data
    public ObservableCollection<ScrapEntry> ScrapEntries { get; }
    public event EventHandler<int>? ScrapCountChanged;

    // Add nodes to scrap
    public void AddToScrap(string filePath, List<DialogNode> nodes,
        string operation = "deleted",
        Dictionary<DialogNode, (int level, DialogNode? parent)>? hierarchyInfo = null)

    // Retrieve node from scrap (without removing)
    public DialogNode? GetNodeFromScrap(string entryId)

    // Remove entry after successful restoration
    public void RemoveFromScrap(string entryId)

    // Clear all scrap for specific file
    public void ClearScrapForFile(string filePath)

    // Clear all scrap entries
    public void ClearAllScrap()

    // Get scrap count for file
    public int GetScrapCount(string filePath)

    // Get scrap entries for file
    public List<ScrapEntry> GetScrapForFile(string filePath)
}
```

### Scrap Entry Structure

```csharp
public class ScrapEntry
{
    public string Id { get; set; }                    // GUID for restoration
    public string FilePath { get; set; }              // Sanitized file path
    public DateTime Timestamp { get; set; }           // When deleted
    public string Operation { get; set; }             // "deleted", "cut"
    public string NodeType { get; set; }              // "Entry" or "Reply"
    public string NodeText { get; set; }              // Preview text
    public string? Speaker { get; set; }              // Speaker tag
    public int OriginalIndex { get; set; }            // Index in list
    public int NestingLevel { get; set; }             // Depth in tree
    public string? ParentNodeText { get; set; }       // Parent preview
    public string SerializedNode { get; set; }        // Full JSON
}
```

## Restoration Process

### Step-by-Step Flow

```
User selects scrap entry in UI
  ↓
MainViewModel.RestoreFromScrap(entryId, selectedParent)
  ├─→ 1. Validate parent selection
  │     └─→ Cannot restore to ROOT without parent
  │
  ├─→ 2. Retrieve node from scrap (without removing yet)
  │     └─→ ScrapManager.GetNodeFromScrap(entryId)
  │         └─→ Deserialize SerializedNode JSON to DialogNode
  │
  ├─→ 3. Type compatibility check
  │     └─→ Cannot restore NPC under NPC or PC under PC
  │
  ├─→ 4. Add node to dialog lists
  │     └─→ dialog.Entries.Add(node) or dialog.Replies.Add(node)
  │
  ├─→ 5. Create pointer from parent to restored node
  │     └─→ new DialogPtr { Node = node, Index = nodeIndex, IsLink = false }
  │
  ├─→ 6. Register pointer with LinkRegistry
  │     └─→ dialog.LinkRegistry.RegisterLink(pointer)
  │
  ├─→ 7. Recalculate indices
  │     └─→ RecalculatePointerIndices()
  │
  ├─→ 8. Remove from scrap (only after successful restoration)
  │     └─→ ScrapManager.RemoveFromScrap(entryId)
  │
  └─→ 9. Refresh tree view
        └─→ RefreshTreeView()
```

### Restoration Validation

**Type Compatibility:**
```csharp
// Aurora rule: Conversations must alternate NPC ↔ PC
if (parentNode.Type == restoredNode.Type)
{
    StatusMessage = "Cannot restore {type} under {type} - must alternate NPC/PC";
    return false;
}
```

**ROOT Restoration:**
```csharp
if (parent is TreeViewRootNode)
{
    // Only Entry nodes can be at ROOT
    if (restoredNode.Type != DialogNodeType.Entry)
    {
        StatusMessage = "Only Entry nodes can be restored to ROOT";
        return false;
    }
}
```

### Metadata Preservation

Scrap entries preserve hierarchy metadata for better UX:

```
Scrap Tab Display:
┌─────────────────────────────────────────┐
│ [Owner] "Hello there!"                  │
│   ↳ Level 2, Parent: [Owner] "Greeting"│
│   ↳ Deleted: 2025-11-15 10:30 AM       │
└─────────────────────────────────────────┘
```

User can see:
- Original nesting level
- Parent node context
- When it was deleted

## RemoveOrphanedPointers()

After deletion, this cleanup step removes pointers that reference deleted nodes:

```csharp
private void RemoveOrphanedPointers()
{
    if (CurrentDialog == null) return;

    int removedCount = 0;

    // Clean Start pointers
    var startsToRemove = new List<DialogPtr>();
    foreach (var start in CurrentDialog.Starts)
    {
        if (start.Node != null && !CurrentDialog.Entries.Contains(start.Node))
        {
            startsToRemove.Add(start);
            LogWarn($"Removing orphaned Start pointer to '{start.Node.DisplayText}'");
        }
    }
    foreach (var start in startsToRemove)
    {
        CurrentDialog.Starts.Remove(start);
        removedCount++;
    }

    // Clean Entry pointers
    foreach (var entry in CurrentDialog.Entries)
    {
        var ptrsToRemove = new List<DialogPtr>();
        foreach (var ptr in entry.Pointers)
        {
            if (ptr.Node != null)
            {
                var list = ptr.Type == DialogNodeType.Entry ?
                    CurrentDialog.Entries : CurrentDialog.Replies;

                if (!list.Contains(ptr.Node))
                {
                    ptrsToRemove.Add(ptr);
                    LogWarn($"Removing orphaned pointer from Entry '{entry.DisplayText}' to '{ptr.Node.DisplayText}'");
                }
            }
        }
        foreach (var ptr in ptrsToRemove)
        {
            entry.Pointers.Remove(ptr);
            removedCount++;
        }
    }

    // Clean Reply pointers (same pattern as Entry)
    // ... similar logic ...

    if (removedCount > 0)
    {
        LogInfo($"Removed {removedCount} orphaned pointers");
    }
}
```

**Why Separate Step?**

Deletion can create orphaned pointers when:
- Shared node is deleted from one branch but referenced from another
- Link target is deleted
- Parent-child link child is deleted

RemoveOrphanedPointers() ensures `.dlg` file has no dangling pointers.

---

[↑ Back to Top](#orphan-detection-and-scrap-system)
