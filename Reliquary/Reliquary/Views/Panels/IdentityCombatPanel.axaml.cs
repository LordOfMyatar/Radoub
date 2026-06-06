using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PlaceableEditor.Views.Panels;

public partial class IdentityCombatPanel : UserControl
{
    public IdentityCombatPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
