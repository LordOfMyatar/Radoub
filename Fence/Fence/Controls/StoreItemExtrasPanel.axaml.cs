using Avalonia.Controls;

namespace MerchantEditor.Controls;

/// <summary>
/// Fence-only companion to the shared <c>ItemDetailsPanel</c>. Displays
/// store-listing fields (sell price, buy price, infinite flag, store panel)
/// for the currently selected <see cref="MerchantEditor.ViewModels.StoreItemViewModel"/>.
/// Read-only: the proper two-way <c>StoreItemExtrasViewModel</c> is tracked as
/// follow-up tech-debt — see plan doc for the sprint that introduced this control.
/// </summary>
public partial class StoreItemExtrasPanel : UserControl
{
    public StoreItemExtrasPanel()
    {
        InitializeComponent();
    }
}
