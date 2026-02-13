using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

public partial class FactionEditorWindow : Window
{
    private readonly FactionEditorViewModel _viewModel;

    public FactionEditorWindow()
    {
        InitializeComponent();
        _viewModel = new FactionEditorViewModel();
        DataContext = _viewModel;

        _viewModel.MatrixChanged += OnMatrixChanged;
        Opened += OnWindowOpened;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;
        _viewModel.SetParentWindow(this);
        await _viewModel.LoadFacFileAsync();
    }

    private void OnMatrixChanged(object? sender, EventArgs e)
    {
        BuildMatrixGrid();
    }

    private void BuildMatrixGrid()
    {
        MatrixContainer.Children.Clear();
        MatrixContainer.ColumnDefinitions.Clear();
        MatrixContainer.RowDefinitions.Clear();

        int n = _viewModel.FactionCount;
        if (n == 0) return;

        // Create grid: (n+1) rows and (n+1) columns (header row/col + data)
        // Column 0: row headers. Row 0: column headers.
        MatrixContainer.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel)); // Row header
        for (int col = 0; col < n; col++)
        {
            MatrixContainer.ColumnDefinitions.Add(new ColumnDefinition(70, GridUnitType.Pixel));
        }

        MatrixContainer.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Column header
        for (int row = 0; row < n; row++)
        {
            MatrixContainer.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Top-left corner: label
        var cornerLabel = new TextBlock
        {
            Text = "Perceiver ▼",
            FontSize = 11,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(4, 0, 4, 4)
        };
        Grid.SetRow(cornerLabel, 0);
        Grid.SetColumn(cornerLabel, 0);
        MatrixContainer.Children.Add(cornerLabel);

        // Column headers (perceived faction names)
        for (int col = 0; col < n; col++)
        {
            var faction = _viewModel.Factions[col];
            var header = new TextBlock
            {
                Text = faction.Name,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 4),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 66
            };
            ToolTip.SetTip(header, $"Perceived: {faction.Name} (Index {faction.Index})");
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, col + 1);
            MatrixContainer.Children.Add(header);
        }

        // Row headers and cells
        for (int row = 0; row < n; row++)
        {
            var faction = _viewModel.Factions[row];

            // Row header
            var rowHeader = new TextBlock
            {
                Text = faction.Name,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 92
            };
            ToolTip.SetTip(rowHeader, $"Perceiver: {faction.Name} (Index {faction.Index})");
            Grid.SetRow(rowHeader, row + 1);
            Grid.SetColumn(rowHeader, 0);
            MatrixContainer.Children.Add(rowHeader);

            // Cells
            for (int col = 0; col < n; col++)
            {
                var cell = _viewModel.GetCell(row, col);
                if (cell == null) continue;

                var cellControl = CreateCellControl(cell);
                Grid.SetRow(cellControl, row + 1);
                Grid.SetColumn(cellControl, col + 1);
                MatrixContainer.Children.Add(cellControl);
            }
        }
    }

    private Control CreateCellControl(MatrixCellViewModel cell)
    {
        var converter = ReputationColorConverter.Instance;

        if (cell.IsDiagonal)
        {
            // Diagonal: non-editable, show "---"
            var border = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(1),
                MinHeight = 32,
                [!Border.BackgroundProperty] = new Binding(nameof(cell.ReputationValue))
                {
                    Source = cell,
                    Converter = converter
                }
            };

            var text = new TextBlock
            {
                Text = "---",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.3
            };

            border.Child = text;
            return border;
        }

        // Editable cell: Border with TextBox
        var cellBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(1),
            MinHeight = 32,
            [!Border.BackgroundProperty] = new Binding(nameof(cell.ReputationValue))
            {
                Source = cell,
                Converter = converter
            }
        };

        var textBox = new TextBox
        {
            MinWidth = 40,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(2),
            Tag = cell,
            [!TextBox.TextProperty] = new Binding(nameof(cell.ReputationText))
            {
                Source = cell,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            }
        };

        // Tooltip showing the relationship
        var perceiver = _viewModel.Factions.ElementAtOrDefault(cell.Row);
        var perceived = _viewModel.Factions.ElementAtOrDefault(cell.Col);
        if (perceiver != null && perceived != null)
        {
            ToolTip.SetTip(cellBorder,
                $"{perceiver.Name} sees {perceived.Name}");
        }

        textBox.KeyDown += OnCellTextBoxKeyDown;

        cellBorder.Child = textBox;
        return cellBorder;
    }

    private void OnCellTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox && textBox.Tag is MatrixCellViewModel cell)
        {
            // Force binding update on Enter
            cell.ReputationText = textBox.Text ?? "0";
            e.Handled = true;

            // Move to next cell
            var nextCol = cell.Col + 1;
            var nextRow = cell.Row;
            if (nextCol >= _viewModel.FactionCount)
            {
                nextCol = 0;
                nextRow++;
            }
            // Skip diagonal
            if (nextRow == nextCol) nextCol++;
            if (nextCol >= _viewModel.FactionCount)
            {
                nextCol = 0;
                nextRow++;
            }

            FocusCell(nextRow, nextCol);
        }
    }

    private void FocusCell(int row, int col)
    {
        if (row >= _viewModel.FactionCount) return;

        foreach (var child in MatrixContainer.Children)
        {
            if (Grid.GetRow(child) == row + 1 && Grid.GetColumn(child) == col + 1
                && child is Border border && border.Child is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
                break;
            }
        }
    }

    private void OnFactionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateParentComboBox();
        UpdateRemoveButton();
    }

    private void UpdateParentComboBox()
    {
        var selected = _viewModel.SelectedFaction;
        if (selected == null) return;

        var items = new List<ParentFactionItem>
        {
            new("(None)", 0xFFFFFFFF)
        };

        foreach (var f in _viewModel.Factions)
        {
            if (f.Index == 0) continue; // PC cannot be parent
            if (f.Index == selected.Index) continue; // Cannot be own parent
            items.Add(new ParentFactionItem(f.Name, (uint)f.Index));
        }

        ParentComboBox.ItemsSource = items;
        ParentComboBox.SelectedItem = items.FirstOrDefault(i => i.Id == selected.ParentFactionId)
                                      ?? items[0];
    }

    private void UpdateRemoveButton()
    {
        RemoveFactionButton.IsEnabled = _viewModel.CanRemoveFaction(_viewModel.SelectedFaction);
    }

    private void OnParentSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.SelectedFaction == null) return;
        if (ParentComboBox.SelectedItem is ParentFactionItem item)
        {
            _viewModel.SelectedFaction.ParentFactionId = item.Id;
        }
    }

    private void OnAddFactionClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new AddFactionDialog(_viewModel.Factions.ToList());
        dialog.Closed += (_, _) =>
        {
            if (dialog.Confirmed)
            {
                _viewModel.AddFaction(dialog.FactionName, dialog.IsGlobal, dialog.ParentId);
            }
        };
        dialog.Show(this);
    }

    private async void OnRemoveFactionClick(object? sender, RoutedEventArgs e)
    {
        var selected = _viewModel.SelectedFaction;
        if (selected == null || !_viewModel.CanRemoveFaction(selected)) return;

        var confirm = new ConfirmDialog(
            "Remove Faction",
            $"Remove faction \"{selected.Name}\"?\n\nThis will also remove all reputation entries for this faction.");
        await confirm.ShowDialog(this);

        if (confirm.Confirmed)
        {
            _viewModel.RemoveFaction(selected);
        }
    }

    private record ParentFactionItem(string Name, uint Id)
    {
        public override string ToString() => Name;
    }
}
