using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Uti;

namespace ItemEditor.Services;

/// <summary>
/// Pure model-mutation helpers for the item property list with refresh-failure rollback (#2258).
///
/// Each operation mutates the <see cref="List{ItemProperty}"/>, runs a UI <c>refresh</c> callback,
/// and rolls the model back to its pre-call state if the refresh throws — mirroring the canonical
/// TryAddProperty pattern (#2166). UI controls (combo selections, tree expansion) can hold stale
/// state across a refresh and crash deep in the Avalonia render loop; rolling back keeps the
/// model consistent with what the user sees instead of leaving orphaned/lost properties.
///
/// All methods return <c>true</c> when the mutation was applied and the refresh succeeded,
/// <c>false</c> when nothing was done (no-op input) or the refresh threw and was rolled back.
/// The refresh exception is swallowed here so callers can report via the status bar; the caller
/// supplies a refresh delegate that does its own logging.
/// </summary>
public static class PropertyListMutator
{
    /// <summary>
    /// Append <paramref name="toAdd"/> to <paramref name="properties"/>, then refresh.
    /// On refresh failure, removes exactly the appended entries.
    /// </summary>
    public static bool BatchAdd(
        List<ItemProperty> properties,
        IReadOnlyList<ItemProperty> toAdd,
        Action refresh)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));
        if (toAdd == null) throw new ArgumentNullException(nameof(toAdd));
        if (refresh == null) throw new ArgumentNullException(nameof(refresh));
        if (toAdd.Count == 0) return false;

        int originalCount = properties.Count;
        properties.AddRange(toAdd);

        try
        {
            refresh();
            return true;
        }
        catch
        {
            // Roll back: truncate the appended entries so the model matches the pre-add state.
            properties.RemoveRange(originalCount, properties.Count - originalCount);
            return false;
        }
    }

    /// <summary>
    /// Remove the entries at <paramref name="indices"/> from <paramref name="properties"/>, then refresh.
    /// On refresh failure, re-inserts the removed entries at their original positions.
    /// </summary>
    public static bool RemoveAt(
        List<ItemProperty> properties,
        IReadOnlyList<int> indices,
        Action refresh)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (refresh == null) throw new ArgumentNullException(nameof(refresh));

        // Capture (index, value) for valid indices in descending order so removal does not shift
        // later targets. Ascending re-insert on rollback restores original positions.
        var removed = indices
            .Where(i => i >= 0 && i < properties.Count)
            .Distinct()
            .OrderByDescending(i => i)
            .Select(i => (Index: i, Value: properties[i]))
            .ToList();

        if (removed.Count == 0) return false;

        foreach (var (index, _) in removed)
            properties.RemoveAt(index);

        try
        {
            refresh();
            return true;
        }
        catch
        {
            foreach (var (index, value) in removed.OrderBy(r => r.Index))
                properties.Insert(index, value);
            return false;
        }
    }

    /// <summary>
    /// Re-insert <paramref name="entries"/> (each an index + value) into <paramref name="properties"/>,
    /// then refresh. The inverse of <see cref="RemoveAt"/>: entries are inserted in ascending index
    /// order so each lands at its original position. On refresh failure, removes exactly the
    /// re-inserted entries so the model returns to its pre-call state.
    ///
    /// Used by undo of a remove/clear so the restore runs through the same rollback-on-refresh-failure
    /// seam as the forward mutation (#2231 / #2258).
    /// </summary>
    public static bool InsertAt(
        List<ItemProperty> properties,
        IReadOnlyList<(int Index, ItemProperty Value)> entries,
        Action refresh)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        if (refresh == null) throw new ArgumentNullException(nameof(refresh));
        if (entries.Count == 0) return false;

        // Insert ascending so earlier indices are filled before later ones shift into place.
        var ordered = entries.OrderBy(e => e.Index).ToList();
        var inserted = new List<int>();
        foreach (var (index, value) in ordered)
        {
            int at = Math.Min(index, properties.Count);
            properties.Insert(at, value);
            inserted.Add(at);
        }

        try
        {
            refresh();
            return true;
        }
        catch
        {
            // Roll back: remove the just-inserted entries (descending so indices stay valid).
            foreach (var at in inserted.OrderByDescending(i => i))
                if (at < properties.Count) properties.RemoveAt(at);
            return false;
        }
    }

    /// <summary>
    /// Replace the entry at <paramref name="index"/> with <paramref name="replacement"/>, then refresh.
    /// On refresh failure, restores the original entry. Used by the #2406 edit-apply path so an edit
    /// runs through the same rollback-on-refresh-failure seam as add/remove/clear (#2258 / #2166).
    /// Returns <c>false</c> (no change, no refresh) when the index is out of range.
    /// </summary>
    public static bool ReplaceAt(
        List<ItemProperty> properties,
        int index,
        ItemProperty replacement,
        Action refresh)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));
        if (refresh == null) throw new ArgumentNullException(nameof(refresh));
        if (index < 0 || index >= properties.Count) return false;

        var original = properties[index];
        properties[index] = replacement;

        try
        {
            refresh();
            return true;
        }
        catch
        {
            properties[index] = original;
            return false;
        }
    }

    /// <summary>
    /// Clear <paramref name="properties"/>, then refresh.
    /// On refresh failure, restores the original list contents in order.
    /// </summary>
    public static bool ClearAll(
        List<ItemProperty> properties,
        Action refresh)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));
        if (refresh == null) throw new ArgumentNullException(nameof(refresh));
        if (properties.Count == 0) return false;

        var snapshot = new List<ItemProperty>(properties);
        properties.Clear();

        try
        {
            refresh();
            return true;
        }
        catch
        {
            properties.AddRange(snapshot);
            return false;
        }
    }
}
