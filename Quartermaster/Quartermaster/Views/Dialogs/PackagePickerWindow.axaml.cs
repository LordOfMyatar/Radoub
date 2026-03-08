using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Quartermaster.Views.Dialogs;

public partial class PackagePickerWindow : Window
{
    private readonly ListBox _packagesListBox;
    private readonly Button _okButton;
    private readonly TextBlock _infoLabel;
    private readonly TextBox _searchBox;
    private readonly List<PackageItem> _packages;
    private List<PackageItem> _filteredPackages;
    private readonly byte _currentPackageId;

    public bool Confirmed { get; private set; }
    public byte? SelectedPackageId { get; private set; }

    public PackagePickerWindow() : this(new List<(byte Id, string Name)>(), 0)
    {
    }

    public PackagePickerWindow(List<(byte Id, string Name)> packages, byte currentPackageId)
    {
        InitializeComponent();

        _packagesListBox = this.FindControl<ListBox>("PackagesListBox")!;
        _okButton = this.FindControl<Button>("OkButton")!;
        _infoLabel = this.FindControl<TextBlock>("InfoLabel")!;
        _searchBox = this.FindControl<TextBox>("PackageSearchBox")!;
        _currentPackageId = currentPackageId;

        // Convert to display items
        _packages = packages.Select(p => new PackageItem { Id = p.Id, Name = p.Name }).ToList();
        _filteredPackages = new List<PackageItem>(_packages);

        // Set item source
        _packagesListBox.ItemsSource = _filteredPackages;

        // Select current package if it exists
        var currentItem = _filteredPackages.FirstOrDefault(p => p.Id == currentPackageId);
        if (currentItem != null)
        {
            _packagesListBox.SelectedItem = currentItem;
            _packagesListBox.ScrollIntoView(currentItem);
        }

        // Update info label
        _infoLabel.Text = $"{packages.Count} packages loaded from packages.2da";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var filter = _searchBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(filter))
        {
            _filteredPackages = new List<PackageItem>(_packages);
        }
        else
        {
            _filteredPackages = _packages
                .Where(p => p.Name.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
                         || p.Id.ToString().Contains(filter))
                .ToList();
        }

        _packagesListBox.ItemsSource = _filteredPackages;
        _infoLabel.Text = _filteredPackages.Count == _packages.Count
            ? $"{_packages.Count} packages loaded from packages.2da"
            : $"Showing {_filteredPackages.Count} of {_packages.Count} packages";

        // Re-select current package if still visible
        var currentItem = _filteredPackages.FirstOrDefault(p => p.Id == _currentPackageId);
        if (currentItem != null)
            _packagesListBox.SelectedItem = currentItem;
    }

    private void OnPackageSelected(object? sender, SelectionChangedEventArgs e)
    {
        _okButton.IsEnabled = _packagesListBox.SelectedItem != null;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_packagesListBox.SelectedItem is PackageItem item)
        {
            SelectedPackageId = item.Id;
            Confirmed = true;
            Close();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private class PackageItem
    {
        public byte Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
