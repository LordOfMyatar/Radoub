using System.Collections.Generic;
using System.Linq;

namespace ItemEditor.Services;

/// <summary>
/// Snapshot of which top-level Available Properties tree nodes are expanded,
/// keyed by PropertyIndex. Used to restore expansion state across a tree
/// rebuild so the user does not have to re-expand a category after each Add (#2227).
/// </summary>
public sealed class TreeExpansionSnapshot
{
    public static readonly TreeExpansionSnapshot Empty = new(new HashSet<int>());

    public IReadOnlySet<int> ExpandedPropertyIndices { get; }

    internal TreeExpansionSnapshot(IReadOnlySet<int> expandedPropertyIndices)
    {
        ExpandedPropertyIndices = expandedPropertyIndices;
    }

    public bool ShouldExpand(int propertyIndex) => ExpandedPropertyIndices.Contains(propertyIndex);
}

public static class TreeExpansionTracker
{
    public static TreeExpansionSnapshot Capture(IEnumerable<int> expandedPropertyIndices)
    {
        if (expandedPropertyIndices == null)
            return TreeExpansionSnapshot.Empty;

        var set = new HashSet<int>(expandedPropertyIndices);
        return new TreeExpansionSnapshot(set);
    }
}
