using System;
using MerchantEditor.ViewModels;
using Radoub.UI.Undo;

namespace MerchantEditor.Commands;

/// <summary>
/// Records an inline ResRef edit on a single store item as one undo step. The DataGrid's TwoWay
/// binding writes the new value to the item before the host builds this command, so Do() re-applies
/// the new value (idempotent, correct redo) and Undo restores the captured old value. Records nothing
/// if old and new are equal (Do returns false).
/// </summary>
public sealed class SetResRefCommand : IUndoableCommand
{
    private readonly StoreItemViewModel _item;
    private readonly string _oldValue;
    private readonly string _newValue;

    public SetResRefCommand(StoreItemViewModel item, string oldValue, string newValue)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _oldValue = oldValue ?? string.Empty;
        _newValue = newValue ?? string.Empty;
    }

    public string Description => $"edit ResRef {_newValue}";

    public bool Do()
    {
        if (_oldValue == _newValue) return false; // no change → don't record
        _item.ResRef = _newValue;
        return true;
    }

    public void Undo() => _item.ResRef = _oldValue;
}
