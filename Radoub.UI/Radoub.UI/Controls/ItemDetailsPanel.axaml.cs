using Avalonia.Controls;

namespace Radoub.UI.Controls;

/// <summary>
/// Read-only item details panel shared across Radoub tools.
/// Bound to a <see cref="Radoub.UI.ViewModels.ItemViewModel"/> via <c>DataContext</c>;
/// shows a "no item selected" placeholder when the DataContext is null.
/// Tool-specific extras (e.g., Fence sell/buy/infinite/panel) live in companion controls.
/// </summary>
public partial class ItemDetailsPanel : UserControl
{
    public ItemDetailsPanel()
    {
        InitializeComponent();
    }
}
