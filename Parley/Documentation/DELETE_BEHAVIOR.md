# Delete Operation Behavior in Parley

**Status**: Improved behavior over Aurora Toolset (as of v0.1.0-alpha)

## Overview

Parley uses reference-counting logic to determine when nodes should be deleted. This prevents accidental data loss when multiple conversation paths share the same dialog nodes.

**Key Principle**: A node is only deleted when ALL pointers to it are removed.

## How It Works

### Reference Counting

When you delete a node, Parley:
1. Counts all incoming pointers to child nodes
2. Only deletes children if `otherIncomingLinks == 0`
3. Preserves shared nodes referenced by other conversation paths

### Visual Example

```mermaid
graph TD
    Start1[START: Entry 1<br/>"Hi there"] --> N25[Node 25<br/>"End greeting"]
    Start2[START: Entry 2<br/>"Let's continue"] --> N26[Node 26<br/>"Middle section"]
    Start3[START: Entry 3<br/>"Final thoughts"] --> N51[Node 51<br/>"Conclusion"]

    N25 --> N26
    N26 --> N50[Node 50<br/>"Transition"]
    N50 --> N51

    style Start2 fill:#ff6b6b
    style Start2 stroke:#c92a2a
```

**Before Delete**: 3 conversation starts, nodes 26-50 form middle section, node 51 starts final section

```mermaid
graph TD
    Start1[START: Entry 1<br/>"Hi there"] --> N25[Node 25<br/>"End greeting"]
    Start3[START: Entry 3<br/>"Final thoughts"] --> N51[Node 51<br/>"Conclusion"]

    N25 --> N26[Node 26<br/>"Middle section"]
    N26 --> N50[Node 50<br/>"Transition"]
    N50 --> N51

    style N26 fill:#51cf66
    style N50 fill:#51cf66
    style N51 fill:#51cf66
```

**After Deleting Entry 2 START**: Nodes 26-51 preserved because Entry 1 still references node 26

## Parley vs Aurora

| Scenario | Aurora Behavior | Parley Behavior |
|----------|----------------|-----------------|
| Delete node with shared children | Orphans shared nodes | Preserves shared nodes |
| Delete START with external refs | Breaks pointer chain | Maintains accessibility |
| Shared reply between entries | Can cause corruption | Protected by reference counting |

## Common Scenarios

### Scenario 1: Shared Reply (Original Issue #6)

```mermaid
graph LR
    E1["Entry 1<br/>'Anything else?'"] --> R[Reply<br/>'I need gear']
    E2["Entry 2<br/>'Sure, what kind?'"] --> R

    style R fill:#ffd43b
```

**Deleting Entry 1**:
- Aurora: Reply disappears, Entry 2 broken
- Parley: Reply preserved (Entry 2 still references it)

### Scenario 2: Linear Chain with No Sharing

```mermaid
graph LR
    S[START] --> E1[Entry 1] --> R1[Reply 1] --> E2[Entry 2] --> R2[Reply 2]
```

**Deleting START or Entry 1**:
- Aurora: Entire chain deleted
- Parley: Entire chain deleted (no external references)

### Scenario 3: Multiple STARTs to Same Node

```mermaid
graph LR
    S1[START 1] --> E[Entry<br/>'Common greeting']
    S2[START 2] --> E
```

**Deleting START 1**:
- Aurora: Entry orphaned if START 1 was original
- Parley: Entry preserved (START 2 still references it)

## Technical Details

### DeleteNodeRecursive Logic

From `MainViewModel.cs:1058-1070`:

```csharp
if (otherIncomingLinks == 0)
{
    // No other nodes reference this child, safe to delete recursively
    DeleteNodeRecursive(ptr.Node);
}
else
{
    // This child is shared by other nodes, don't delete it
    UnifiedLogger.LogApplication(LogLevel.DEBUG,
        $"Skipping deletion of shared child (has {otherIncomingLinks} other references)");
}
```

### LinkRegistry System

- Tracks all `DialogPtr` references between nodes
- Provides `GetLinksTo(node)` to count incoming references
- Ensures accurate reference counting during delete operations

## Benefits

1. **Data Safety**: Prevents accidental loss of shared dialog nodes
2. **Predictable**: Consistent behavior regardless of delete order
3. **Aurora Compatible**: Exported files work correctly in NWN
4. **Better UX**: No unexpected disappearances during editing

## Limitations

**Deep Tree Deletion**: Practical limit of ~30 levels for complex shared structures. Professional BioWare dialogs typically stay under 15 levels. See [KNOWN_ISSUES.md](KNOWN_ISSUES.md) for details.

## User Workflow

### When You Want to Delete a Shared Node

1. **Delete all references first**: Remove pointers from all parent nodes
2. **Delete the node last**: Once all incoming pointers removed, node can be deleted
3. **Or use Cut/Paste**: Moving nodes automatically updates references

### Visual Feedback

- TreeView shows all START points
- Shared nodes appear under multiple parents (via link indicators)
- Properties panel shows incoming reference count (future enhancement)

## Testing

See `Parley.Tests/DeleteOperationTests.cs` for comprehensive test coverage:
- `Delete_NodeWithSharedReplies_PreservesOtherNodes`
- `Delete_MultipleStartsPointingToSameNode_PreservesNode`
- `Delete_LinearChain_RemovesAllNodes`

## Related Documentation

- [DEEP_TREE_LIMITATION.md](DEEP_TREE_LIMITATION.md) - Depth limitation details
- Issue #6 - Original bug report and fix
- LinkRegistry system - Reference tracking implementation
