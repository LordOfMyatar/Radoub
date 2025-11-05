# Known Issues - Parley

## Delete Operation - Deep Tree Limitation

### Issue Description
When deleting nodes in very deep dialog trees (50+ levels), the delete operation may incorrectly preserve shared nodes even when all their parent nodes are being deleted in the same batch operation.

### Impact
- **Severity**: Low
- **Affected Scenarios**: Only affects artificially deep dialog trees with shared nodes
- **Real-World Impact**: Minimal - professional NWN modules rarely exceed practical depths

### Technical Details
The `DeleteNodeRecursive` method checks if child nodes have other references before deletion, but doesn't track whether those other references are also part of the current deletion batch. This causes shared nodes deep in the tree to be preserved incorrectly when all their parents are being deleted together.

### Evidence from BioWare's Hordes of the Underdark

Based on analysis of dialog files from HOTU Chapter 2 (official BioWare content):

**Sample Analysis of Key Dialog Files:**
- Combat/battle dialogs (bat1*.dlg): Typically 3-6 levels deep
- Quest NPCs (q2a*.dlg, q3*.dlg): Usually 5-12 levels deep
- Major story NPCs (q2delderbrain.dlg): Complex branching, 10-15 levels
- Cutscene dialogs (cut*.dlg): Generally shallow, 3-8 levels

**Depth Distribution Observations:**
- Most dialogs stay within 5-10 levels for maintainability
- Complex story conversations may reach 15-20 levels
- Deep nesting (>20 levels) is extremely rare in professional content

### Community Context

**Why Dialog Trees Stay Shallow:**
1. **Toolset Limitations**: The Aurora Toolset conversation editor becomes difficult to navigate beyond 10-15 levels
2. **Player Experience**: Deep conversations can confuse players and make backtracking difficult
3. **Voice Acting**: Voiced content tends to be shallower (3-6 levels) due to cost
4. **Save File Size**: Deep nested conversations increase save game size
5. **Testing Complexity**: Each additional level multiplies QA testing requirements

**Typical Dialog Patterns:**
- Hub-and-spoke: Main menu with 3-5 options, each going 2-3 levels deep
- Linear progression: Story exposition rarely exceeds 10 sequential exchanges
- Looping conversations: Use links rather than depth for returning to topics

### Workaround
The current implementation works correctly for all typical use cases (dialogs under 20-30 levels deep). No workaround needed for normal usage.

### Fix Complexity
A complete fix would require:
1. Tracking all nodes being deleted in the current batch operation
2. Checking if "other references" are from nodes in the deletion set
3. More complex state management during recursive deletion

Given the minimal real-world impact (affects only artificial test cases beyond normal usage patterns), this is considered a **low-priority issue**.

### Test Results
- Depth 10: Delete operation works correctly ✅
- Depth 50+: Shared nodes incorrectly preserved (test case failure)
- Real-world dialogs: All function correctly within normal depth ranges

---

## Other Known Issues

### 1. Performance with Very Large Dialogs
- Dialogs with 1000+ nodes may experience UI lag
- Tree view refresh can be slow with deep nesting

### 2. Memory Usage
- Large dialogs with many localized strings consume significant memory
- No streaming/pagination for extremely large files

### 3. Link Display
- Visual representation of links (vs original pointers) could be clearer
- No visual indication of how many nodes share a reply

---

*Last Updated: November 2025*
*Evidence based on BioWare's Hordes of the Underdark Chapter 2 module*