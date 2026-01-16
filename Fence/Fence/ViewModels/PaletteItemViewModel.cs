using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MerchantEditor.ViewModels;

/// <summary>
/// ViewModel for an item in the palette (available items to add to store).
/// </summary>
public class PaletteItemViewModel : INotifyPropertyChanged
{
    private string _resRef = string.Empty;
    private string _displayName = string.Empty;
    private string _baseItemType = string.Empty;
    private int _baseValue;
    private bool _isStandard = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ResRef
    {
        get => _resRef;
        set { if (_resRef != value) { _resRef = value; OnPropertyChanged(); } }
    }

    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName != value) { _displayName = value; OnPropertyChanged(); } }
    }

    public string BaseItemType
    {
        get => _baseItemType;
        set { if (_baseItemType != value) { _baseItemType = value; OnPropertyChanged(); } }
    }

    public int BaseValue
    {
        get => _baseValue;
        set { if (_baseValue != value) { _baseValue = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// True if this is a standard/base game item, false if custom content.
    /// </summary>
    public bool IsStandard
    {
        get => _isStandard;
        set { if (_isStandard != value) { _isStandard = value; OnPropertyChanged(); } }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
