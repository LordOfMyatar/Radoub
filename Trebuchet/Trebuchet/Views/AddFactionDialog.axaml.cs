using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

public partial class AddFactionDialog : Window
{
    public bool Confirmed { get; private set; }
    public string FactionName { get; private set; } = string.Empty;
    public bool IsGlobal { get; private set; } = true;
    public uint ParentId { get; private set; } = 0xFFFFFFFF;

    public AddFactionDialog()
    {
        InitializeComponent();
    }

    public AddFactionDialog(List<FactionViewModel> existingFactions) : this()
    {
        var items = new List<ParentItem> { new("(None)", 0xFFFFFFFF) };
        foreach (var f in existingFactions)
        {
            items.Add(new ParentItem(f.Name, (uint)f.Index));
        }

        ParentComboBox.ItemsSource = items;
        ParentComboBox.SelectedIndex = 0;

        NameTextBox.TextChanged += (_, _) =>
        {
            OkButton.IsEnabled = !string.IsNullOrWhiteSpace(NameTextBox.Text);
        };
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        FactionName = NameTextBox.Text?.Trim() ?? string.Empty;
        IsGlobal = GlobalCheckBox.IsChecked == true;
        ParentId = (ParentComboBox.SelectedItem as ParentItem)?.Id ?? 0xFFFFFFFF;
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private record ParentItem(string Name, uint Id)
    {
        public override string ToString() => Name;
    }
}
