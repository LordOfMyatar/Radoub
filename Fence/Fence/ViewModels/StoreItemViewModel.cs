using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MerchantEditor.ViewModels;

/// <summary>
/// ViewModel for an item in the store inventory.
/// </summary>
public class StoreItemViewModel : INotifyPropertyChanged
{
    private string _resRef = string.Empty;
    private string _displayName = string.Empty;
    private bool _infinite;
    private int _panelId;
    private string _baseItemType = string.Empty;
    private int _sellPrice;
    private int _buyPrice;

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

    public bool Infinite
    {
        get => _infinite;
        set { if (_infinite != value) { _infinite = value; OnPropertyChanged(); } }
    }

    public int PanelId
    {
        get => _panelId;
        set { if (_panelId != value) { _panelId = value; OnPropertyChanged(); } }
    }

    public string BaseItemType
    {
        get => _baseItemType;
        set { if (_baseItemType != value) { _baseItemType = value; OnPropertyChanged(); } }
    }

    public int SellPrice
    {
        get => _sellPrice;
        set { if (_sellPrice != value) { _sellPrice = value; OnPropertyChanged(); } }
    }

    public int BuyPrice
    {
        get => _buyPrice;
        set { if (_buyPrice != value) { _buyPrice = value; OnPropertyChanged(); } }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
