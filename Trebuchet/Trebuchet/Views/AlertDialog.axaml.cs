using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RadoubLauncher.Views;

public partial class AlertDialog : Window
{
    public AlertDialog()
    {
        InitializeComponent();
    }

    public AlertDialog(string title, string message) : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
