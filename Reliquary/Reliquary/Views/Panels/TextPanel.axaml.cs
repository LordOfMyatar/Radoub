using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Radoub.UI.Controls;
using Radoub.UI.Services;

namespace PlaceableEditor.Views.Panels;

/// <summary>
/// Text panel (design §5.3): localized Description (shown in-game on examine) and builder-only
/// Comments. Both are spell-checked via <see cref="SpellCheckTextBox"/> (Radoub.Dictionary).
/// The Description gets a right-click "Insert Token" menu (#2075 4-tab picker) since it is the
/// player-facing field; Comments are never shown in-game so they carry no token menu.
/// Both fields bind two-way to <c>PlaceableViewModel.Description</c> / <c>Comment</c>.
/// </summary>
public partial class TextPanel : UserControl
{
    private readonly QuickTokenService _quickTokenService = new();

    public TextPanel()
    {
        InitializeComponent();
        WireTokenMenu(this.FindControl<SpellCheckTextBox>("DescriptionTextBox"));
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Append the shared "Insert Token" submenu to the field's right-click menu.</summary>
    private void WireTokenMenu(SpellCheckTextBox? textBox)
    {
        if (textBox == null) return;
        textBox.ContextMenuExtras = menu =>
            TokenContextMenu.AppendTokenMenu(menu, textBox,
                () => _ = TokenInsertionHelper.OpenTokenWindowAsync(
                    textBox, this.VisualRoot as Window),
                _quickTokenService);
    }
}
