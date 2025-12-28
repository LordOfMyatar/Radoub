using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Quartermaster.Views.Panels;

public partial class PlaceholderPanel : UserControl
{
    public static readonly StyledProperty<string> SectionNameProperty =
        AvaloniaProperty.Register<PlaceholderPanel, string>(nameof(SectionName), "Section");

    public string SectionName
    {
        get => GetValue(SectionNameProperty);
        set => SetValue(SectionNameProperty, value);
    }

    public PlaceholderPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SectionNameProperty)
        {
            var titleBlock = this.FindControl<TextBlock>("SectionTitle");
            if (titleBlock != null)
            {
                titleBlock.Text = SectionName;
            }
        }
    }
}
