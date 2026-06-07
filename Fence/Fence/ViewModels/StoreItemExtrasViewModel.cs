using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MerchantEditor.ViewModels;

/// <summary>
/// Read-only projection of a <see cref="StoreItemViewModel"/>'s store-listing fields
/// (sell price, buy price, infinite flag, store panel) for the
/// <see cref="MerchantEditor.Controls.StoreItemExtrasPanel"/> (#2153).
///
/// Replaces the previous arrangement where the panel bound directly to the grid-row
/// <see cref="StoreItemViewModel"/>. This dedicated VM owns only the store-listing
/// concern and keeps the panel's DataContext separate from the inventory grid's VM.
///
/// The VM mirrors the source and stays live: it forwards the source's PropertyChanged
/// so panel display updates when the source changes (e.g. the context-menu Infinite
/// toggle mutates the source). Sell/Buy remain computed values owned by the source —
/// this VM only exposes them; the markup/markdown calculation stays in MainWindow.
/// </summary>
public class StoreItemExtrasViewModel : INotifyPropertyChanged
{
    private readonly StoreItemViewModel _source;

    public StoreItemExtrasViewModel(StoreItemViewModel source)
    {
        _source = source;
        _source.PropertyChanged += OnSourcePropertyChanged;
    }

    public int SellPrice => _source.SellPrice;
    public int BuyPrice => _source.BuyPrice;
    public bool Infinite => _source.Infinite;
    public int PanelId => _source.PanelId;

    /// <summary>
    /// Stop tracking the source. Call when the panel's DataContext changes so the old
    /// VM does not hold the source alive or keep forwarding events.
    /// </summary>
    public void Detach()
    {
        _source.PropertyChanged -= OnSourcePropertyChanged;
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Re-raise only the store-listing properties this VM projects.
        switch (e.PropertyName)
        {
            case nameof(StoreItemViewModel.SellPrice):
                OnPropertyChanged(nameof(SellPrice));
                break;
            case nameof(StoreItemViewModel.BuyPrice):
                OnPropertyChanged(nameof(BuyPrice));
                break;
            case nameof(StoreItemViewModel.Infinite):
                OnPropertyChanged(nameof(Infinite));
                break;
            case nameof(StoreItemViewModel.PanelId):
                OnPropertyChanged(nameof(PanelId));
                break;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
