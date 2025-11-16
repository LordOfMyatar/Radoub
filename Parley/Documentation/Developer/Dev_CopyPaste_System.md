# Copy/Paste System Architecture

**Developer Documentation**
**Last Updated**: 2025-11-15
**Related Issues**: #6, #111, #121, #122

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Copy vs Cut Semantics](#copy-vs-cut-semantics)
- [Paste Operations](#paste-operations)
- [Deep Cloning Algorithm](#deep-cloning-algorithm)
- [Link Handling](#link-handling)
- [Type Validation Rules](#type-validation-rules)
- [Common Pitfalls](#common-pitfalls)

## Overview

The Copy/Paste system manages clipboard operations for dialog nodes in Parley. It handles three primary operations:

1. **Copy** - Clone node and descendants to clipboard
2. **Cut** - Copy + mark source for deletion
3. **Paste** - Create duplicate (new nodes) or link (pointer to existing node)

**Key Services:**
- `DialogClipboardService` - Manages clipboard state and cloning logic
- `MainViewModel.PasteAsDuplicate()` - Handles paste-as-duplicate UI coordination
- `MainViewModel.PasteAsLink()` - Handles paste-as-link UI coordination

## Architecture

### Service Responsibilities

**DialogClipboardService** (business logic):
```
┌─────────────────────────────┐
│ DialogClipboardService      │
├─────────────────────────────┤
│ Fields:                     │
│  - _originalNode  (ref)     │  ← Original node for PasteAsLink
│  - _copiedNode    (clone)   │  ← Cloned node for PasteAsDuplicate
│  - _wasCut        (bool)    │  ← Deletion flag
│  - _sourceDialog  (Dialog)  │  ← Source dialog reference
├─────────────────────────────┤
│ Methods:                    │
│  + CopyNode()               │  ← Stores ref + creates clone
│  + CutNode()                │  ← Copy + set _wasCut flag
│  + PasteAsDuplicate()       │  ← Returns cloned node
│  + PasteAsLink()            │  ← Returns DialogPtr to original
│  - CloneNode()              │  ← Deep clone algorithm
│  - CloneChildNodes()        │  ← Recursive cloning w/ cloneMap
└─────────────────────────────┘
```

**MainViewModel** (UI coordination):
```
┌─────────────────────────────┐
│ MainViewModel               │
├─────────────────────────────┤
│ Methods:                    │
│  + PasteAsDuplicate()       │  ← Type validation, ROOT handling
│  + PasteAsLink()            │  ← Delegation to service
│  + CopyNode()               │  ← Keyboard shortcut handler
│  + CutNode()                │  ← Keyboard shortcut + delete
│  - CloneNode()              │  ← Legacy method (still used)
└─────────────────────────────┘
```

### Data Flow

**Copy Operation:**
```
User: Ctrl+C
  ↓
MainViewModel.CopyNode()
  ↓
DialogClipboardService.CopyNode(node, dialog)
  ↓
_originalNode = node              ← Store reference for linking
_copiedNode = CloneNode(node)     ← Deep clone for pasting
_wasCut = false
_sourceDialog = dialog
```

**Cut Operation:**
```
User: Ctrl+X
  ↓
MainViewModel.CutNode()
  ↓
DialogClipboardService.CutNode(node, dialog)
  ↓
_originalNode = node              ← Store reference for linking
_copiedNode = CloneNode(node)     ← Deep clone for pasting
_wasCut = true                    ← Mark for deletion
_sourceDialog = dialog
  ↓
MainViewModel deletes source node from tree
```

**Paste as Duplicate:**
```
User: Ctrl+V
  ↓
MainViewModel.PasteAsDuplicate(parent)
  ├─→ Validate type compatibility (NPC/PC alternation)
  ├─→ Handle ROOT special cases (NPC Reply → Entry conversion)
  └─→ DialogClipboardService.PasteAsDuplicate()
       ↓
       Returns _copiedNode (already cloned)
       ↓
       Add to dialog.Entries or dialog.Replies
       ↓
       Create DialogPtr from parent to new node
       ↓
       If _wasCut: ClearClipboard()
```

**Paste as Link:**
```
User: Ctrl+Shift+V
  ↓
MainViewModel.PasteAsLink(parent)
  └─→ DialogClipboardService.PasteAsLink()
       ↓
       Validate: Cannot link after Cut (source deleted)
       Validate: Can only link within same dialog
       ↓
       Find _originalNode index in dialog.Entries/Replies
       ↓
       Create DialogPtr with IsLink=true
       ↓
       Add pointer to parent.Pointers
       ↓
       Register with LinkRegistry
```

## Copy vs Cut Semantics

### Current Implementation

**Both operations now create clones:**

```csharp
// Copy
_originalNode = node;           // For PasteAsLink
_copiedNode = CloneNode(node);  // For PasteAsDuplicate
_wasCut = false;

// Cut
_originalNode = node;           // For PasteAsLink
_copiedNode = CloneNode(node);  // For PasteAsDuplicate
_wasCut = true;                 // Marks source for deletion
```

**Benefits:**
- **Consistency** - Both operations behave the same
- **PasteAsLink works with Copy** - Can link to original that stays in place
- **PasteAsLink blocked after Cut** - Cannot link to deleted source
- **Simpler logic** - No special cases for Cut vs Copy

## Paste Operations

### Paste as Duplicate (Ctrl+V)

Creates **new nodes** by adding `_copiedNode` (clone) to dialog.

**Type Validation:**
```csharp
// Aurora Engine rule: Conversations must alternate NPC ↔ PC
if (parentNode.Type == clipboardNode.Type)
{
    // BLOCKED: Cannot paste NPC under NPC or PC under PC
    StatusMessage = "Cannot paste {type} under {type} - must alternate NPC/PC";
    return;
}
```

**ROOT Special Cases:**

1. **PC Reply → Blocked**
   ```csharp
   if (clipboardNode.Type == Reply && string.IsNullOrEmpty(clipboardNode.Speaker))
   {
       // PC Replies cannot be at ROOT (only respond to NPCs)
       StatusMessage = "Cannot paste PC Reply to ROOT";
       return;
   }
   ```

2. **NPC Reply → Auto-convert to Entry**
   ```csharp
   if (clipboardNode.Type == Reply && !string.IsNullOrEmpty(clipboardNode.Speaker))
   {
       // NPC Reply with Speaker set → convert to Entry for ROOT
       clipboardNode.Type = DialogNodeType.Entry;
       StatusMessage = "Auto-converted NPC Reply to Entry for ROOT level";
   }
   ```

**Index Recalculation:**

After pasting, indices must be recalculated because deep cloning can add multiple nodes:

```csharp
RecalculatePointerIndices();  // Critical after paste operations
```

### Paste as Link (Ctrl+Shift+V)

Creates **pointer to existing node** without duplicating content.

**Validation Rules:**

1. **Cannot link after Cut:**
   ```csharp
   if (_wasCut)
   {
       // Source node will be deleted - cannot create link to it
       return null;
   }
   ```

2. **Same dialog only:**
   ```csharp
   if (_sourceDialog != currentDialog)
   {
       // Links across dialogs not supported (different node lists)
       return null;
   }
   ```

3. **Original node must exist:**
   ```csharp
   int index = dialog.Entries.IndexOf(_originalNode);
   if (index < 0)
   {
       // Node not found in dialog - cannot create link
       return null;
   }
   ```

**Link Creation:**
```csharp
var linkPtr = new DialogPtr
{
    Parent = dialog,
    Type = _originalNode.Type,
    Index = (uint)index,        // Index of ORIGINAL node
    IsLink = true,              // Critical flag
    Node = _originalNode        // Reference to original
};

parentNode.Pointers.Add(linkPtr);
dialog.LinkRegistry.RegisterLink(linkPtr);
```

## Deep Cloning Algorithm

### Manual Cloning Approach

**Two-Phase Approach:**

**Phase 1: Shallow clone WITHOUT Pointers**
```csharp
var shallowClone = new DialogNode
{
    Type = original.Type,
    Text = CloneLocString(original.Text),
    Speaker = original.Speaker ?? string.Empty,
    Comment = original.Comment ?? string.Empty,
    Sound = original.Sound ?? string.Empty,
    ScriptAction = original.ScriptAction ?? string.Empty,
    Animation = original.Animation,
    AnimationLoop = original.AnimationLoop,
    Delay = original.Delay,
    Quest = original.Quest ?? string.Empty,
    QuestEntry = original.QuestEntry,
    ActionParams = new Dictionary<string, string>(original.ActionParams ?? new()),
    Pointers = new List<DialogPtr>()  // Empty - populated in Phase 2
};
```

**Phase 2: Recursively rebuild Pointers**
```csharp
CloneChildNodes(original, shallowClone, new Dictionary<DialogNode, DialogNode>(), depth: 0);
```

### Circular Reference Handling

**CloneMap Pattern:**

```csharp
private void CloneChildNodes(DialogNode originalParent, DialogNode cloneParent,
    Dictionary<DialogNode, DialogNode> cloneMap, int depth)
{
    // Depth protection
    if (depth >= 100)
    {
        LogWarning("Max depth reached - stopping recursion");
        return;
    }

    foreach (var originalPtr in originalParent.Pointers)
    {
        DialogNode clonedChild;

        // Check if already cloned (handles circular references)
        if (cloneMap.ContainsKey(originalPtr.Node))
        {
            clonedChild = cloneMap[originalPtr.Node];  // Reuse existing clone
        }
        else
        {
            // Create new clone
            clonedChild = new DialogNode { /* properties */ };
            cloneMap[originalPtr.Node] = clonedChild;  // Track it

            // Recurse
            CloneChildNodes(originalPtr.Node, clonedChild, cloneMap, depth + 1);
        }

        // Create pointer to cloned child
        var clonedPtr = new DialogPtr
        {
            Type = originalPtr.Type,
            Index = originalPtr.Index,  // Will be updated when added to dialog
            IsLink = originalPtr.IsLink,  // Preserve link flag
            Node = clonedChild,
            ScriptAppears = originalPtr.ScriptAppears,
            ConditionParams = new Dictionary<string, string>(originalPtr.ConditionParams ?? new())
        };

        cloneParent.Pointers.Add(clonedPtr);
    }
}
```

**When CloneMap Gets Called:**
- During Copy operation (clones entire subtree)
- During Cut operation (clones entire subtree before deletion)
- NOT during PasteAsLink (just creates pointer, no cloning)

**Key Points:**
- `cloneMap` prevents infinite loops when same node appears multiple times in tree
- Each unique node cloned exactly once
- Nodes referenced multiple times share the same cloned instance
- MAX_DEPTH=100 prevents stack overflow
- IsLink flags are preserved in cloned pointers

## Link Handling

### Display in Tree View

Links are displayed differently from regular nodes to match NWN Toolset behavior:

**TreeViewSafeNode.PopulateChildren():**
```csharp
foreach (var pointer in _originalNode.Pointers)
{
    if (pointer.IsLink)
    {
        // Links shown as TreeViewSafeLinkNode (gray, no expansion)
        var linkNode = new TreeViewSafeLinkNode(pointer.Node, depth + 1, "Link", pointer);
        _children.Add(linkNode);
    }
    else if (_ancestorNodes.Contains(pointer.Node))
    {
        // Circular reference - also shown as link
        var linkNode = new TreeViewSafeLinkNode(pointer.Node, depth + 1, "Circular");
        _children.Add(linkNode);
    }
    else
    {
        // Regular child - expand fully
        var childNode = new TreeViewSafeNode(pointer.Node, newAncestors, depth + 1, pointer);
        _children.Add(childNode);
    }
}
```

**Link Node Characteristics:**
- Gray color (`NodeColor = "Gray"`)
- `IsChild` property returns true (from `SourcePointer.IsLink`)
- No expansion (Children returns null)
- No expand arrow (HasChildren returns false)

### Link vs Circular Reference

**Links (IsLink=true):**
- User-created via Paste as Link
- Intentional sharing of dialog content
- Same node used in multiple conversation paths

**Circular References:**
- Detected via ancestor chain tracking
- Prevents infinite recursion in tree view
- Shows where conversation loops back to earlier node

## Type Validation Rules

### Aurora Engine Constraints

Neverwinter Nights dialog files must follow strict type alternation:

```
Conversation Flow:
┌─────────────────────────────┐
│ Entry (NPC)                 │
│   ├─→ Reply (PC)            │  ← Player response
│   │     └─→ Entry (NPC)     │  ← NPC continues
│   └─→ Reply (PC)            │  ← Another player option
│         └─→ Entry (NPC)     │
└─────────────────────────────┘
```

**Invalid Patterns:**
```
Entry → Entry     ❌ (NPC talking to themselves)
Reply → Reply     ❌ (PC talking to themselves)
```

### Speaker Tag Interpretation

**Entry Nodes:**
- Speaker = "" → Owner (default NPC)
- Speaker = "NPC_NAME" → Specific NPC

**Reply Nodes:**
- Speaker = "" → PC (player character)
- Speaker = "NPC_NAME" → NPC (special case for multi-NPC convos)

### ROOT Node Rules

Only Entry nodes can be at ROOT (conversation starters):

```csharp
// Valid ROOT nodes:
Entry + Speaker=""         → Owner conversation starter
Entry + Speaker="NPC"      → Specific NPC conversation starter

// Invalid ROOT nodes:
Reply + Speaker=""         → ❌ PC cannot start conversations
Reply + Speaker="NPC"      → ✅ Auto-converted to Entry
```

## Common Pitfalls

### 1. Forgetting to Recalculate Indices

**Problem:**
```csharp
var newNode = CloneNode(clipboardNode);
dialog.Entries.Add(newNode);

var ptr = new DialogPtr
{
    Index = 0,  // ❌ WRONG - might not be at index 0
    Node = newNode
};
```

**Solution:**
```csharp
var newNode = CloneNode(clipboardNode);
dialog.Entries.Add(newNode);

var index = (uint)dialog.Entries.IndexOf(newNode);  // ✅ Get actual index
var ptr = new DialogPtr
{
    Index = index,
    Node = newNode
};

RecalculatePointerIndices();  // ✅ Update all pointers after adding nodes
```

### 2. Using ClipboardNode Instead of OriginalNode

**Problem:**
```csharp
// PasteAsLink trying to find cloned node in dialog
int index = dialog.Entries.IndexOf(_copiedNode);  // ❌ Returns -1 (not in list)
```

**Solution:**
```csharp
// Use original node reference
int index = dialog.Entries.IndexOf(_originalNode);  // ✅ Found in list
```

### 3. Not Checking IsLink Before Expanding Children

**Problem:**
```csharp
// PopulateChildren treating links as regular nodes
if (_ancestorNodes.Contains(pointer.Node))
{
    // Show as circular link
}
else
{
    // ❌ WRONG - expands link as full child
    var child = new TreeViewSafeNode(pointer.Node, ancestors, depth + 1);
}
```

**Solution:**
```csharp
// Check IsLink FIRST
if (pointer.IsLink)
{
    // ✅ Show as link (gray, no expansion)
    var linkNode = new TreeViewSafeLinkNode(pointer.Node, depth + 1, "Link", pointer);
}
else if (_ancestorNodes.Contains(pointer.Node))
{
    // Show as circular link
}
else
{
    // Expand as full child
    var child = new TreeViewSafeNode(pointer.Node, ancestors, depth + 1);
}
```

### 4. Allowing PasteAsLink After Cut

**Problem:**
```csharp
// Cut operation
CutNode(node, dialog);

// User tries to paste as link
PasteAsLink(parent);  // ❌ Source node will be deleted!
```

**Solution:**
```csharp
public DialogPtr? PasteAsLink(Dialog dialog, DialogNode? parentNode)
{
    if (_wasCut)
    {
        // ✅ Block PasteAsLink after Cut
        LogWarning("Cannot paste as link after Cut - source will be deleted");
        return null;
    }
    // ... rest of logic
}
```

### 5. Not Validating Type Compatibility

**Problem:**
```csharp
// Paste NPC Entry under NPC Entry parent
parentNode.Pointers.Add(new DialogPtr { Node = npcNode });  // ❌ Invalid conversation
```

**Solution:**
```csharp
if (parentNode.Type == clipboardNode.Type)
{
    // ✅ Block invalid paste
    string parentType = parentNode.Type == Entry ? "NPC" : "PC";
    string childType = clipboardNode.Type == Entry ? "NPC" : "PC";
    StatusMessage = $"Cannot paste {childType} under {parentType} - must alternate";
    return;
}
```

---

[↑ Back to Top](#copy-paste-system-architecture)
