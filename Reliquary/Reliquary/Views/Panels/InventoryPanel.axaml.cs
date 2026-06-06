using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PlaceableEditor.Views.Panels;

public partial class InventoryPanel : UserControl
{
    public InventoryPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
