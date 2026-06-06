using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PlaceableEditor.Views.Panels;

public partial class BehaviorPanel : UserControl
{
    public BehaviorPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
