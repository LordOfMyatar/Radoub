using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MerchantEditor.ViewModels;

namespace MerchantEditor.Commands;

/// <summary>
/// The buy mode for a store's buy restrictions.
/// </summary>
public enum BuyMode
{
    All,
    WillOnlyBuy,
    WillNotBuy
}

/// <summary>
/// An immutable snapshot of the buy-restrictions state: the buy <see cref="Mode"/> plus the set of
/// selected base-item-type indices. Buy restrictions are interrelated (choosing "Buy All" clears the
/// selection), so undo treats the whole state as one unit rather than recording each checkbox edit.
/// <see cref="Capture"/> reads the current state and <see cref="ApplyTo"/> restores it onto the
/// selectable item-type collection; the host applies the <see cref="Mode"/> to the radio buttons.
/// </summary>
public sealed class BuyRestrictionsSnapshot : IEquatable<BuyRestrictionsSnapshot>
{
    public BuyMode Mode { get; }
    public IReadOnlySet<int> SelectedIndices { get; }

    public BuyRestrictionsSnapshot(BuyMode mode, IEnumerable<int> selectedIndices)
    {
        Mode = mode;
        SelectedIndices = new HashSet<int>(selectedIndices ?? Enumerable.Empty<int>());
    }

    /// <summary>Capture the current mode + selected item-type indices.</summary>
    public static BuyRestrictionsSnapshot Capture(
        BuyMode mode, IEnumerable<SelectableBaseItemTypeViewModel> types)
        => new(mode, (types ?? Enumerable.Empty<SelectableBaseItemTypeViewModel>())
            .Where(t => t.IsSelected)
            .Select(t => t.BaseItemIndex));

    /// <summary>Restore the selected state onto the item-type collection (mode is applied by the host).</summary>
    public void ApplyTo(IEnumerable<SelectableBaseItemTypeViewModel> types)
    {
        foreach (var t in types ?? Enumerable.Empty<SelectableBaseItemTypeViewModel>())
            t.IsSelected = SelectedIndices.Contains(t.BaseItemIndex);
    }

    public bool Equals(BuyRestrictionsSnapshot? other)
        => other != null && Mode == other.Mode && SelectedIndices.SetEquals(other.SelectedIndices);

    public override bool Equals(object? obj) => Equals(obj as BuyRestrictionsSnapshot);

    public override int GetHashCode()
    {
        var hash = (int)Mode;
        foreach (var i in SelectedIndices.OrderBy(x => x))
            hash = HashCode.Combine(hash, i);
        return hash;
    }
}
