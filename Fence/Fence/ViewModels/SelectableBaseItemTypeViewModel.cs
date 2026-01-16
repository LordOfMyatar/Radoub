using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MerchantEditor.ViewModels;

/// <summary>
/// ViewModel for a selectable base item type checkbox.
/// </summary>
public class SelectableBaseItemTypeViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    /// <summary>
    /// Base item type index from baseitems.2da.
    /// </summary>
    public int BaseItemIndex { get; }

    /// <summary>
    /// Display name for the item type.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Whether this item type is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public SelectableBaseItemTypeViewModel(int baseItemIndex, string displayName)
    {
        BaseItemIndex = baseItemIndex;
        DisplayName = displayName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
