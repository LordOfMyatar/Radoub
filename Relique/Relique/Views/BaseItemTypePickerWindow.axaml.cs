using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ItemEditor.Services;
using Radoub.Formats.Services;

namespace ItemEditor.Views;

public partial class BaseItemTypePickerWindow : Window
{
    private readonly ListBox _itemTypesListBox;
    private readonly TextBox _searchTextBox;
    private readonly Button _clearSearchButton;
    private readonly Button _okButton;
    private readonly TextBlock _infoLabel;
    private readonly TextBox _descriptionPreview;
    private readonly List<BaseItemTypeDisplayItem> _allTypes;
    private List<BaseItemTypeDisplayItem> _filteredTypes;

    public bool Confirmed { get; private set; }
    public int? SelectedBaseItemIndex { get; private set; }
    public string SelectedDisplayName { get; private set; } = "";

    public BaseItemTypePickerWindow() : this(new List<BaseItemTypeInfo>())
    {
    }

    public BaseItemTypePickerWindow(List<BaseItemTypeInfo> baseItemTypes, int? currentSelection = null)
    {
        InitializeComponent();

        _itemTypesListBox = this.FindControl<ListBox>("ItemTypesListBox")!;
        _searchTextBox = this.FindControl<TextBox>("SearchTextBox")!;
        _clearSearchButton = this.FindControl<Button>("ClearSearchButton")!;
        _okButton = this.FindControl<Button>("OkButton")!;
        _infoLabel = this.FindControl<TextBlock>("InfoLabel")!;
        _descriptionPreview = this.FindControl<TextBox>("DescriptionPreview")!;

        _allTypes = baseItemTypes
            .Select(t => new BaseItemTypeDisplayItem(t))
            .ToList();
        _filteredTypes = _allTypes;

        _itemTypesListBox.ItemsSource = _filteredTypes;

        // Wire up search
        _searchTextBox.TextChanged += OnSearchTextChanged;
        _clearSearchButton.Click += OnClearSearchClick;

        // Double-click to confirm
        _itemTypesListBox.DoubleTapped += OnItemTypeDoubleTapped;

        // Update info label
        _infoLabel.Text = $"{_allTypes.Count} base item types loaded";

        // Pre-select current item if provided
        if (currentSelection.HasValue)
        {
            var match = _allTypes.FirstOrDefault(t => t.BaseItemIndex == currentSelection.Value);
            if (match != null)
            {
                _itemTypesListBox.SelectedItem = match;
                _itemTypesListBox.ScrollIntoView(match);
            }
        }

        // Focus search box on open
        Opened += (_, _) => _searchTextBox.Focus();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        _searchTextBox.Text = "";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var searchText = _searchTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(searchText))
        {
            _filteredTypes = _allTypes;
        }
        else
        {
            _filteredTypes = _allTypes
                .Where(t => t.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                            t.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                            t.BaseItemIndex.ToString().Contains(searchText))
                .ToList();
        }

        _itemTypesListBox.ItemsSource = _filteredTypes;
        _infoLabel.Text = _filteredTypes.Count == _allTypes.Count
            ? $"{_allTypes.Count} base item types loaded"
            : $"Showing {_filteredTypes.Count} of {_allTypes.Count} types";
    }

    private void OnItemTypeSelected(object? sender, SelectionChangedEventArgs e)
    {
        var selected = _itemTypesListBox.SelectedItem as BaseItemTypeDisplayItem;
        _okButton.IsEnabled = selected != null;

        // Update description preview
        _descriptionPreview.Text = selected?.DescriptionText ?? "";
    }

    private void OnItemTypeDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (_itemTypesListBox.SelectedItem is BaseItemTypeDisplayItem)
        {
            ConfirmSelection();
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void ConfirmSelection()
    {
        if (_itemTypesListBox.SelectedItem is BaseItemTypeDisplayItem item)
        {
            SelectedBaseItemIndex = item.BaseItemIndex;
            SelectedDisplayName = item.DisplayName;
            Confirmed = true;
            Close();
        }
    }
}

/// <summary>
/// Display wrapper for BaseItemTypeInfo that adds ModelType display string for the picker list.
/// </summary>
internal class BaseItemTypeDisplayItem : BaseItemTypeInfo
{
    public string ModelTypeDisplay { get; }

    public BaseItemTypeDisplayItem(BaseItemTypeInfo source)
        : base(source.BaseItemIndex, source.DisplayName, source.Label,
              storePanel: source.StorePanel, modelType: source.ModelType, descriptionText: source.DescriptionText,
              stacking: source.Stacking, chargesStarting: source.ChargesStarting)
    {
        ModelTypeDisplay = source.ModelType switch
        {
            0 => "Simple",
            1 => "Layered",
            2 => "Composite",
            3 => "Armor",
            _ => $"Type {source.ModelType}"
        };
    }
}
