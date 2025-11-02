using System.Collections.Generic;
using DialogEditor.Models;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Tracks index changes during node reordering operations.
    /// Ensures all pointers are updated correctly when nodes move in lists.
    /// </summary>
    public class IndexUpdateTracker
    {
        /// <summary>
        /// Mapping of old index → new index for moved/shifted nodes
        /// </summary>
        public Dictionary<uint, uint> IndexMapping { get; private set; }

        /// <summary>
        /// Type of list being reordered (Entry or Reply)
        /// </summary>
        public DialogNodeType ListType { get; private set; }

        /// <summary>
        /// Original index of node being moved
        /// </summary>
        public uint OldIndex { get; private set; }

        /// <summary>
        /// Target index for moved node
        /// </summary>
        public uint NewIndex { get; private set; }

        public IndexUpdateTracker()
        {
            IndexMapping = new Dictionary<uint, uint>();
        }

        /// <summary>
        /// Calculate index mapping for a move operation.
        /// Updates IndexMapping with all nodes that change position.
        /// </summary>
        /// <param name="oldIdx">Current position of node</param>
        /// <param name="newIdx">Target position of node</param>
        /// <param name="listType">Entry or Reply list</param>
        public void CalculateMapping(uint oldIdx, uint newIdx, DialogNodeType listType)
        {
            OldIndex = oldIdx;
            NewIndex = newIdx;
            ListType = listType;
            IndexMapping.Clear();

            if (newIdx == oldIdx)
            {
                // No movement
                return;
            }

            if (newIdx > oldIdx)
            {
                // Moving DOWN (e.g., index 2 → 4)
                // Node at oldIdx moves to newIdx
                IndexMapping[oldIdx] = newIdx;

                // Nodes between shift UP (decrease by 1)
                // Example: [0,1,2,3,4] → [0,1,3,4,2]
                //          Index 3→2, Index 4→3
                for (uint i = oldIdx + 1; i <= newIdx; i++)
                {
                    IndexMapping[i] = i - 1;
                }
            }
            else // newIdx < oldIdx
            {
                // Moving UP (e.g., index 4 → 2)
                // Node at oldIdx moves to newIdx
                IndexMapping[oldIdx] = newIdx;

                // Nodes between shift DOWN (increase by 1)
                // Example: [0,1,2,3,4] → [0,1,4,2,3]
                //          Index 2→3, Index 3→4
                for (uint i = newIdx; i < oldIdx; i++)
                {
                    IndexMapping[i] = i + 1;
                }
            }
        }

        /// <summary>
        /// Check if an index needs updating based on calculated mapping.
        /// </summary>
        /// <param name="index">Index to check</param>
        /// <param name="updatedIndex">New index if mapping exists</param>
        /// <returns>True if index needs updating</returns>
        public bool TryGetUpdatedIndex(uint index, out uint updatedIndex)
        {
            return IndexMapping.TryGetValue(index, out updatedIndex);
        }

        /// <summary>
        /// Get updated index or return original if no mapping exists.
        /// </summary>
        public uint GetUpdatedIndexOrDefault(uint index)
        {
            return IndexMapping.TryGetValue(index, out uint newIdx) ? newIdx : index;
        }

        /// <summary>
        /// Get count of affected indices (nodes that will move).
        /// </summary>
        public int GetAffectedCount()
        {
            return IndexMapping.Count;
        }

        /// <summary>
        /// Get description of move operation for logging.
        /// </summary>
        public string GetMoveDescription()
        {
            string direction = NewIndex > OldIndex ? "DOWN" : "UP";
            return $"{ListType} [{OldIndex}] → [{NewIndex}] ({direction}), {GetAffectedCount()} indices affected";
        }
    }
}
