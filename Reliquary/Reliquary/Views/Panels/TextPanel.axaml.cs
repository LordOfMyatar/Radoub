using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PlaceableEditor.Views.Panels;

public partial class TextPanel : UserControl
{
    public TextPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
