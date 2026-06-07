using Avalonia.Controls;

namespace MerchantEditor.Controls;

/// <summary>
/// Fence-only companion to the shared <c>ItemDetailsPanel</c>. Displays
/// store-listing fields (sell price, buy price, infinite flag, store panel) for the
/// currently selected store item, bound to a dedicated
/// <see cref="MerchantEditor.ViewModels.StoreItemExtrasViewModel"/> (#2153).
/// Read-only display: sell/buy are computed, infinite/panel are edited elsewhere
/// (grid + context menu); this panel reflects them live via the VM.
/// </summary>
public partial class StoreItemExtrasPanel : UserControl
{
    public StoreItemExtrasPanel()
    {
        InitializeComponent();
    }
}
