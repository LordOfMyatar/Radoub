using System.Collections.Generic;
using System.Linq;

namespace DialogEditor.Models
{
    /// <summary>
    /// Static validation helpers for TreeView node analysis.
    /// Extracted from TreeViewSafeNode.cs for maintainability (#708).
    /// </summary>
    public static class TreeViewValidation
    {
        /// <summary>
        /// Issue #484: Calculate which entry siblings are unreachable.
        /// Aurora engine picks the first NPC entry whose condition passes.
        /// If an entry has no condition script, it always passes, blocking all subsequent siblings.
        /// </summary>
        /// <param name="pointers">List of sibling pointers to analyze</param>
        /// <returns>Set of pointer indices that are unreachable</returns>
        public static HashSet<int> CalculateUnreachableSiblings(IList<DialogPtr> pointers)
        {
            var unreachableIndices = new HashSet<int>();

            // Only applies to Entry type siblings (NPC responses)
            // Reply siblings (PC choices) are all shown to the player
            var entryPointers = pointers
                .Select((ptr, idx) => (ptr, idx))
                .Where(x => x.ptr.Type == DialogNodeType.Entry && !x.ptr.IsLink && x.ptr.Node != null)
                .ToList();

            if (entryPointers.Count <= 1)
                return unreachableIndices; // No siblings to compare

            // Find the first entry without a condition - it blocks everything after it
            bool foundBlocker = false;
            foreach (var (ptr, idx) in entryPointers)
            {
                if (foundBlocker)
                {
                    // Everything after an unconditional entry is unreachable
                    unreachableIndices.Add(idx);
                }
                else if (string.IsNullOrEmpty(ptr.ScriptAppears))
                {
                    // This entry has no condition - it will always be picked
                    // All subsequent siblings become unreachable
                    foundBlocker = true;
                }
            }

            return unreachableIndices;
        }
    }
}
