using Avalonia;
using Avalonia.Controls;
using ItemEditor.ViewModels;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using System;
using System.ComponentModel;
using System.Linq;

namespace ItemEditor.Views;

public partial class MainWindow
{
    // --- Editor Population ---

    private void PopulateEditor()
    {
        if (_currentItem == null)
        {
            EmptyStatePanel.IsVisible = true;
            EditorContent.IsVisible = false;
            AppearanceExpander.IsVisible = false;
            ModelPartsPanel.IsVisible = false;
            ColorsPanel.IsVisible = false;
            ArmorPartsPanel.IsVisible = false;
            PropertyConfigPanel.IsVisible = false;
            AssignedPropertiesList.Items.Clear();
            _selectedPropertyType = null;
            _editingPropertyIndex = -1;
            AddPropertyButton.IsEnabled = false;
            AddCheckedButton.IsEnabled = false;
            EditPropertyButton.IsEnabled = false;
            RemovePropertyButton.IsEnabled = false;
            ClearAllPropertiesButton.IsEnabled = false;
            ItemStatisticsPanel.IsVisible = false;
            _itemViewModel = null;
            EditorContent.DataContext = null;
            return;
        }

        EmptyStatePanel.IsVisible = false;
        EditorContent.IsVisible = true;

        // Create ViewModel and bind (pass TLK resolver for base game items with StrRef names)
        Func<uint, string?>? tlkResolver = _gameDataService?.IsConfigured == true
            ? strRef => _gameDataService.GetString(strRef)
            : null;
        _itemViewModel = new ItemViewModel(_currentItem, tlkResolver);
        EditorContent.DataContext = _itemViewModel;

        // Wire up dirty tracking from ViewModel property changes
        _itemViewModel.PropertyChanged += OnItemPropertyChanged;

        // Select the correct base item type and palette category
        SelectBaseItemInComboBox(_currentItem.BaseItem);
        UpdateConditionalFields(_currentItem.BaseItem);
        SelectPaletteCategoryInComboBox(_currentItem.PaletteID);

        // Populate assigned properties list
        RefreshAssignedProperties();

        // Populate local variables
        PopulateVariables();

        FilePathText.Text = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "";
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isLoading)
        {
            MarkDirty();
        }
    }

    private void SelectBaseItemInComboBox(int baseItemIndex)
    {
        for (int i = 0; i < BaseItemComboBox.Items.Count; i++)
        {
            if (BaseItemComboBox.Items[i] is ComboBoxItem item && item.Tag is int index && index == baseItemIndex)
            {
                _isLoading = true;
                BaseItemComboBox.SelectedIndex = i;
                _isLoading = false;
                return;
            }
        }

        // Base item not in list — add an "Unknown" entry
        var unknownItem = new ComboBoxItem
        {
            Content = $"Unknown ({baseItemIndex})",
            Tag = baseItemIndex
        };
        BaseItemComboBox.Items.Add(unknownItem);
        _isLoading = true;
        BaseItemComboBox.SelectedItem = unknownItem;
        _isLoading = false;
    }

    private void OnBaseItemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _itemViewModel == null) return;
        if (BaseItemComboBox.SelectedItem is ComboBoxItem item && item.Tag is int index)
        {
            _itemViewModel.BaseItem = index;
            UpdateConditionalFields(index);
        }
    }

    private void SelectPaletteCategoryInComboBox(byte paletteId)
    {
        for (int i = 0; i < PaletteCategoryComboBox.Items.Count; i++)
        {
            if (PaletteCategoryComboBox.Items[i] is ComboBoxItem item && item.Tag is byte id && id == paletteId)
            {
                _isLoading = true;
                PaletteCategoryComboBox.SelectedIndex = i;
                _isLoading = false;
                return;
            }
        }
        // Not found — select first item if available
        if (PaletteCategoryComboBox.Items.Count > 0)
        {
            _isLoading = true;
            PaletteCategoryComboBox.SelectedIndex = 0;
            _isLoading = false;
        }
    }

    private void OnPaletteCategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _itemViewModel == null) return;
        if (PaletteCategoryComboBox.SelectedItem is ComboBoxItem item && item.Tag is byte id)
        {
            _itemViewModel.PaletteID = id;
        }
    }

    // --- Conditional Fields ---

    private void UpdateConditionalFields(int baseItemIndex)
    {
        var typeInfo = _baseItemTypes?.FirstOrDefault(t => t.BaseItemIndex == baseItemIndex);

        // Item Type Description from 2DA (read-only)
        BaseItemDescriptionText.Text = typeInfo?.DescriptionText ?? string.Empty;

        // Appearance section: show if any sub-section is visible
        bool showModelParts = typeInfo?.HasModelParts ?? false;
        bool showMultipleParts = typeInfo?.HasMultipleModelParts ?? false;
        bool showColors = typeInfo?.HasColorFields ?? false;
        bool showArmorParts = typeInfo?.HasArmorParts ?? false;

        AppearanceExpander.IsVisible = showModelParts || showColors || showArmorParts;

        // Model Parts: show for types 0, 1, 2 (Simple, Layered, Composite)
        ModelPartsPanel.IsVisible = showModelParts;
        if (showModelParts)
        {
            // Parts 2 & 3 only for Composite (ModelType 2)
            ModelPart2Label.IsVisible = showMultipleParts;
            ModelPart2UpDown.IsVisible = showMultipleParts;
            ModelPart3Label.IsVisible = showMultipleParts;
            ModelPart3UpDown.IsVisible = showMultipleParts;
        }

        // Colors: show for Layered (1) and Armor (3)
        ColorsPanel.IsVisible = showColors;

        // Armor Parts: show for Armor (3) only
        ArmorPartsPanel.IsVisible = showArmorParts;
        if (showArmorParts)
        {
            PopulateArmorPartsGrid();
        }
    }

    private static readonly string[] ArmorPartNames = new[]
    {
        "Torso", "Belt", "Pelvis", "Neck", "Robe",
        "LBicep", "RBicep", "LFArm", "RFArm",
        "LHand", "RHand", "LShoul", "RShoul",
        "LThigh", "RThigh", "LShin", "RShin",
        "LFoot", "RFoot"
    };

    private void PopulateArmorPartsGrid()
    {
        if (_itemViewModel == null) return;

        ArmorPartsGrid.Children.Clear();
        ArmorPartsGrid.RowDefinitions.Clear();

        // Layout: 2 columns of label+value pairs, ~10 rows
        int row = 0;
        for (int i = 0; i < ArmorPartNames.Length; i++)
        {
            var partName = ArmorPartNames[i];
            int col = (i % 2 == 0) ? 0 : 3;
            if (i % 2 == 0)
            {
                ArmorPartsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            var label = new TextBlock
            {
                Text = partName,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 8)
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, col);
            ArmorPartsGrid.Children.Add(label);

            var upDown = new NumericUpDown
            {
                Value = _itemViewModel.GetArmorPart(partName),
                Minimum = 0,
                Maximum = 255,
                FormatString = "N0",
                Margin = new Thickness(0, 0, 0, 8)
            };
            var capturedPartName = partName;
            upDown.ValueChanged += (_, args) =>
            {
                if (_isLoading || _itemViewModel == null) return;
                _itemViewModel.SetArmorPart(capturedPartName, (byte)(args.NewValue ?? 0));
            };
            Grid.SetRow(upDown, row);
            Grid.SetColumn(upDown, col + 1);
            ArmorPartsGrid.Children.Add(upDown);

            if (i % 2 == 1) row++;
        }
        // Handle odd count
        if (ArmorPartNames.Length % 2 == 1) row++;
    }

    // --- Local Variables ---

    private void PopulateVariables()
    {
        VariablesGrid.ItemsSource = null;

        foreach (var vm in _variables)
            vm.PropertyChanged -= OnVariablePropertyChanged;

        _variables.Clear();

        if (_currentItem == null) return;

        foreach (var variable in _currentItem.VarTable)
        {
            var vm = VariableViewModel.FromVariable(variable);
            vm.PropertyChanged += OnVariablePropertyChanged;
            _variables.Add(vm);
        }

        VariablesGrid.ItemsSource = _variables;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {_variables.Count} local variables");
    }

    private void OnVariablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isLoading) MarkDirty();

        // Re-validate on name changes
        if (e.PropertyName == nameof(VariableViewModel.Name))
            ValidateVariablesRealTime();
    }

    private void UpdateVarTable()
    {
        if (_currentItem == null) return;

        _currentItem.VarTable.Clear();
        foreach (var vm in _variables)
        {
            // Skip variables with empty names
            if (!string.IsNullOrWhiteSpace(vm.Name))
                _currentItem.VarTable.Add(vm.ToVariable());
        }
    }

    private string? ValidateVariables()
    {
        var names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emptyCount = 0;

        foreach (var vm in _variables)
        {
            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                emptyCount++;
                continue;
            }

            if (!names.Add(vm.Name))
            {
                return $"Duplicate variable name: \"{vm.Name}\". Each variable must have a unique name.";
            }
        }

        if (emptyCount > 0)
        {
            return $"{emptyCount} variable(s) have no name and will be removed on save.";
        }

        return null;
    }

    private void ValidateVariablesRealTime()
    {
        var nameCounts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in _variables)
        {
            if (string.IsNullOrWhiteSpace(vm.Name)) continue;
            nameCounts.TryGetValue(vm.Name, out var count);
            nameCounts[vm.Name] = count + 1;
        }

        var errors = new System.Collections.Generic.List<string>();
        foreach (var vm in _variables)
        {
            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                vm.HasError = true;
                vm.ErrorMessage = "Variable name is required";
            }
            else if (nameCounts.TryGetValue(vm.Name, out var count) && count > 1)
            {
                vm.HasError = true;
                vm.ErrorMessage = $"Duplicate name: \"{vm.Name}\"";
                if (!errors.Contains($"Duplicate: \"{vm.Name}\""))
                    errors.Add($"Duplicate: \"{vm.Name}\"");
            }
            else
            {
                vm.HasError = false;
                vm.ErrorMessage = string.Empty;
            }
        }

        var emptyCount = _variables.Count(v => string.IsNullOrWhiteSpace(v.Name));
        if (emptyCount > 0)
            errors.Insert(0, $"{emptyCount} variable(s) missing name");

        if (errors.Count > 0)
        {
            VariableValidationText.Text = string.Join(" | ", errors);
            VariableValidationText.IsVisible = true;
        }
        else
        {
            VariableValidationText.IsVisible = false;
        }
    }

    private void OnAddVariable(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentItem == null) return;

        var newVar = new VariableViewModel
        {
            Name = string.Empty,
            Type = VariableType.Int,
            IntValue = 0
        };

        newVar.PropertyChanged += OnVariablePropertyChanged;
        _variables.Add(newVar);
        VariablesGrid.SelectedItem = newVar;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            VariablesGrid.ScrollIntoView(newVar, VariablesGrid.Columns[0]);
            VariablesGrid.BeginEdit();
        }, Avalonia.Threading.DispatcherPriority.Background);

        MarkDirty();
        ValidateVariablesRealTime();
    }

    private void OnRemoveVariable(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedItems = VariablesGrid.SelectedItems?.Cast<VariableViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0) return;

        foreach (var item in selectedItems)
        {
            item.PropertyChanged -= OnVariablePropertyChanged;
            _variables.Remove(item);
        }

        MarkDirty();
        ValidateVariablesRealTime();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Removed {selectedItems.Count} variable(s)");
    }
}
