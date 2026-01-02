using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Quartermaster.Views.Dialogs;

public partial class FactionPickerWindow : Window
{
    private readonly ListBox _factionsListBox;
    private readonly Button _okButton;
    private readonly TextBlock _infoLabel;
    private readonly List<FactionItem> _factions;

    public bool Confirmed { get; private set; }
    public ushort? SelectedFactionId { get; private set; }

    public FactionPickerWindow() : this(new List<(ushort Id, string Name)>(), 0)
    {
    }

    public FactionPickerWindow(List<(ushort Id, string Name)> factions, ushort currentFactionId)
    {
        InitializeComponent();

        _factionsListBox = this.FindControl<ListBox>("FactionsListBox")!;
        _okButton = this.FindControl<Button>("OkButton")!;
        _infoLabel = this.FindControl<TextBlock>("InfoLabel")!;

        // Convert to display items
        _factions = factions.Select(f => new FactionItem { Id = f.Id, Name = f.Name }).ToList();

        // Set item source
        _factionsListBox.ItemsSource = _factions;

        // Select current faction if it exists
        var currentItem = _factions.FirstOrDefault(f => f.Id == currentFactionId);
        if (currentItem != null)
        {
            _factionsListBox.SelectedItem = currentItem;
            _factionsListBox.ScrollIntoView(currentItem);
        }

        // Update info label based on faction count
        if (factions.Count <= 5)
        {
            _infoLabel.Text = "Using default factions (repute.fac not found)";
        }
        else
        {
            _infoLabel.Text = $"{factions.Count} factions loaded from repute.fac";
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnFactionSelected(object? sender, SelectionChangedEventArgs e)
    {
        _okButton.IsEnabled = _factionsListBox.SelectedItem != null;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_factionsListBox.SelectedItem is FactionItem item)
        {
            SelectedFactionId = item.Id;
            Confirmed = true;
            Close();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private class FactionItem
    {
        public ushort Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
