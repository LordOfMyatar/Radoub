using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ItemEditor.ViewModels;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using System;
using System.Collections.Generic;
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
            IconChooserPanel.IsVisible = false;
            SelectedIconPreview.Source = null;
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
            BindItemPreview(null);
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

        // Wire 3D preview to the new VM (#1908 PR3b)
        BindItemPreview(_itemViewModel);

        // Display the correct base item type and palette category
        DisplayBaseItemType(_currentItem.BaseItem);
        UpdateConditionalFields(_currentItem.BaseItem);
        SelectPaletteCategoryInComboBox(_currentItem.PaletteID);

        // Populate assigned properties list
        RefreshAssignedProperties();

        // Populate local variables
        PopulateVariables();

        // Set initial identified visual cue state
        UpdateIdentifiedVisualCue();

        StatusBar.FilePath = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : null;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isLoading)
        {
            MarkDirty();

            // Refresh statistics when base item type changes (#1804)
            if (e.PropertyName == "BaseItem")
            {
                RefreshStatistics();
            }

            // Update unidentified description visual cue when Identified flag changes (#1810)
            if (e.PropertyName == "Identified")
            {
                UpdateIdentifiedVisualCue();
            }

            // Recompute Armor Class when the Torso part changes (#1908 follow-up).
            if (e.PropertyName == "ArmorPart_Torso")
            {
                UpdateArmorClassDisplay();
            }
        }
    }

    /// <summary>
    /// Show derived Armor Class for the current item. Per the Aurora item format spec,
    /// armor AC = ACBONUS column of parts_chest.2da at the row indicated by ArmorPart_Torso.
    /// Read-only — AC is derived, not stored on the UTI.
    /// </summary>
    private void UpdateArmorClassDisplay()
    {
        if (_itemViewModel == null || _gameDataService == null || !_gameDataService.IsConfigured)
        {
            ArmorClassText.Text = "—";
            return;
        }

        var torsoPart = _itemViewModel.GetArmorPart("Torso");
        var acBonus = _gameDataService.Get2DAValue("parts_chest", torsoPart, "ACBONUS");

        if (string.IsNullOrEmpty(acBonus) || acBonus == "****")
        {
            ArmorClassText.Text = "—";
            return;
        }

        ArmorClassText.Text = acBonus;
    }

    private void DisplayBaseItemType(int baseItemIndex)
    {
        var typeInfo = _baseItemTypes?.FirstOrDefault(t => t.BaseItemIndex == baseItemIndex);
        BaseItemTypeTextBox.Text = typeInfo != null
            ? typeInfo.DisplayName
            : $"Unknown ({baseItemIndex})";
    }

    private async void OnBrowseBaseItemTypeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_itemViewModel == null || _baseItemTypes == null) return;

        var picker = new BaseItemTypePickerWindow(_baseItemTypes, _itemViewModel.BaseItem);
        await picker.ShowDialog(this);

        if (picker.Confirmed && picker.SelectedBaseItemIndex.HasValue)
        {
            _itemViewModel.BaseItem = picker.SelectedBaseItemIndex.Value;
            DisplayBaseItemType(picker.SelectedBaseItemIndex.Value);
            UpdateConditionalFields(picker.SelectedBaseItemIndex.Value);
            PopulateAvailableProperties(PropertySearchBox.Text); // Refresh for new base item type (#1972)
        }
    }

    private void OnBaseItemTypeTextBoxClicked(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        OnBrowseBaseItemTypeClick(sender, new Avalonia.Interactivity.RoutedEventArgs());
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

        // Stack Size / Charges: enable based on Stacking + ChargesStarting columns (#1814)
        // Stackable items: show Stack Size, grey out Charges (can't have both)
        // Charge-based items (wands/rods): show Charges, grey out Stack Size
        // All other items: both enabled (any item can get charge-based properties)
        bool isStackable = typeInfo?.IsStackable ?? false;
        bool isChargeBased = typeInfo?.HasCharges ?? false;

        StackSizeUpDown.IsEnabled = !isChargeBased;  // Disabled only for charge items
        ChargesUpDown.IsEnabled = !isStackable;       // Disabled only for stackable items

        string disabledTip = "Not applicable for this base item type";
        ToolTip.SetTip(StackSizeUpDown, StackSizeUpDown.IsEnabled ? null : disabledTip);
        ToolTip.SetTip(ChargesUpDown, ChargesUpDown.IsEnabled ? null : disabledTip);
        ToolTip.SetTip(StackSizeLabel, StackSizeUpDown.IsEnabled ? null : disabledTip);
        ToolTip.SetTip(ChargesLabel, ChargesUpDown.IsEnabled ? null : disabledTip);

        // Appearance section: show if any sub-section is visible
        bool showModelParts = typeInfo?.HasModelParts ?? false;
        bool showMultipleParts = typeInfo?.HasMultipleModelParts ?? false;
        bool showColors = typeInfo?.HasColorFields ?? false;
        bool showArmorParts = typeInfo?.HasArmorParts ?? false;

        // Icon chooser: show when icon service is available and base type has model parts
        bool showIconChooser = _itemIconService != null && showModelParts;
        IconChooserPanel.IsVisible = showIconChooser;
        if (showIconChooser)
        {
            UpdateIconPreview(baseItemIndex);
        }

        AppearanceExpander.IsVisible = showModelParts || showColors || showArmorParts || showIconChooser;

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
        if (showColors)
        {
            UpdateAllColorSwatches();
        }

        // Armor Parts: show for Armor (3) only
        ArmorPartsPanel.IsVisible = showArmorParts;
        if (showArmorParts)
        {
            PopulateArmorPartsGrid();
        }
    }

    // Order matches the BioWare Aurora toolset's Appearance tab: anatomical top-to-bottom,
    // left/right paired, robe last. Each entry is the GFF armor-part field key (UTI stores
    // these as ArmorPart_<Name> fields).
    private static readonly string[] ArmorPartNames = new[]
    {
        "Neck",
        "Torso",
        "Belt",
        "Pelvis",
        "RShoul", "LShoul",
        "RBicep", "LBicep",
        "RFArm",  "LFArm",
        "RHand",  "LHand",
        "RThigh", "LThigh",
        "RShin",  "LShin",
        "RFoot",  "LFoot",
        "Robe",
    };

    // User-friendly display labels for armor part dropdown rows.
    private static readonly Dictionary<string, string> ArmorPartLabels = new(StringComparer.Ordinal)
    {
        ["Neck"] = "Neck",
        ["Torso"] = "Torso",
        ["Belt"] = "Belt",
        ["Pelvis"] = "Pelvis",
        ["RShoul"] = "Right Shoulder",
        ["LShoul"] = "Left Shoulder",
        ["RBicep"] = "Right Bicep",
        ["LBicep"] = "Left Bicep",
        ["RFArm"] = "Right Forearm",
        ["LFArm"] = "Left Forearm",
        ["RHand"] = "Right Hand",
        ["LHand"] = "Left Hand",
        ["RThigh"] = "Right Thigh",
        ["LThigh"] = "Left Thigh",
        ["RShin"] = "Right Shin",
        ["LShin"] = "Left Shin",
        ["RFoot"] = "Right Foot",
        ["LFoot"] = "Left Foot",
        ["Robe"] = "Robe",
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
                Text = ArmorPartLabels.TryGetValue(partName, out var friendly) ? friendly : partName,
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

        UpdateArmorClassDisplay();
    }

    // --- Icon Preview + Picker ---

    private void UpdateIconPreview(int baseItemIndex)
    {
        SelectedIconPreview.Source = null;

        if (_itemIconService == null || _itemViewModel == null)
        {
            IconChooserInfoLabel.Text = "(no game data)";
            return;
        }

        byte currentModelPart1 = _itemViewModel.ModelPart1;
        var icon = _itemIconService.GetItemIcon(baseItemIndex, currentModelPart1);
        if (icon == null)
            icon = _itemIconService.GetItemIcon(baseItemIndex);

        if (icon != null)
        {
            SelectedIconPreview.Source = icon;
            IconChooserInfoLabel.Text = $"Model {currentModelPart1}";
        }
        else
        {
            IconChooserInfoLabel.Text = "(no icon)";
        }
    }

    private async void OnBrowseIconClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_gameDataService == null || _itemIconService == null || _itemViewModel == null || _currentItem == null)
            return;

        var baseItemIndex = _currentItem.BaseItem;
        var typeInfo = _baseItemTypes?.Find(t => t.BaseItemIndex == baseItemIndex);
        var baseItemName = typeInfo?.DisplayName ?? $"Type {baseItemIndex}";
        int invW = typeInfo?.InvSlotWidth ?? 1;
        int invH = typeInfo?.InvSlotHeight ?? 1;

        var picker = new ItemIconPickerWindow(
            _gameDataService, _itemIconService, baseItemIndex,
            _itemViewModel.ModelPart1, baseItemName, invW, invH);

        var result = await picker.ShowDialog<byte?>(this);
        if (result.HasValue)
        {
            _itemViewModel.ModelPart1 = result.Value;
            UpdateIconPreview(baseItemIndex);
        }
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

    // --- Identified Visual Cue (#1810) ---

    private void UpdateIdentifiedVisualCue()
    {
        bool isIdentified = _itemViewModel?.Identified ?? false;
        UnidentifiedDescriptionPanel.Opacity = isIdentified ? 0.5 : 1.0;
        IdentifiedHintLabel.IsVisible = isIdentified;
    }

    // --- Color Picker ---

    private void UpdateAllColorSwatches()
    {
        if (_paletteColorService == null || _itemViewModel == null) return;

        UpdateColorSwatch(Cloth1ColorSwatch, PaletteColorService.Palettes.Cloth1, _itemViewModel.Cloth1Color);
        UpdateColorSwatch(Cloth2ColorSwatch, PaletteColorService.Palettes.Cloth2, _itemViewModel.Cloth2Color);
        UpdateColorSwatch(Leather1ColorSwatch, PaletteColorService.Palettes.Leather1, _itemViewModel.Leather1Color);
        UpdateColorSwatch(Leather2ColorSwatch, PaletteColorService.Palettes.Leather2, _itemViewModel.Leather2Color);
        UpdateColorSwatch(Metal1ColorSwatch, PaletteColorService.Palettes.Metal1, _itemViewModel.Metal1Color);
        UpdateColorSwatch(Metal2ColorSwatch, PaletteColorService.Palettes.Metal2, _itemViewModel.Metal2Color);
    }

    private void UpdateColorSwatch(Avalonia.Controls.Border swatch, string paletteName, byte colorIndex)
    {
        if (_paletteColorService == null)
        {
            swatch.Background = BrushManager.GetDisabledBrush(this);
            return;
        }
        swatch.Background = _paletteColorService.CreateGradientBrush(paletteName, colorIndex);
    }

    private async void OpenColorPicker(string paletteName, byte currentIndex, string dialogTitle, Action<byte> onColorSelected)
    {
        if (_paletteColorService == null) return;

        var picker = new Radoub.UI.Views.ColorPickerWindow(_paletteColorService, paletteName, currentIndex, dialogTitle);
        await picker.ShowDialog(this);

        if (picker.Confirmed)
        {
            onColorSelected(picker.SelectedColorIndex);
        }
    }

    private void OnCloth1ColorBrowse(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_itemViewModel == null) return;
        OpenColorPicker(PaletteColorService.Palettes.Cloth1, _itemViewModel.Cloth1Color, "Select Cloth 1 Color", newIndex =>
        {
            _itemViewModel.Cloth1Color = newIndex;
            UpdateColorSwatch(Cloth1ColorSwatch, PaletteColorService.Palettes.Cloth1, newIndex);
        });
    }

    private void OnCloth2ColorBrowse(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_itemViewModel == null) return;
        OpenColorPicker(PaletteColorService.Palettes.Cloth2, _itemViewModel.Cloth2Color, "Select Cloth 2 Color", newIndex =>
        {
            _itemViewModel.Cloth2Color = newIndex;
            UpdateColorSwatch(Cloth2ColorSwatch, PaletteColorService.Palettes.Cloth2, newIndex);
        });
    }

    private void OnLeather1ColorBrowse(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_itemViewModel == null) return;
        OpenColorPicker(PaletteColorService.Palettes.Leather1, _itemViewModel.Leather1Color, "Select Leather 1 Color", newIndex =>
        {
            _itemViewModel.Leather1Color = newIndex;
            UpdateColorSwatch(Leather1ColorSwatch, PaletteColorService.Palettes.Leather1, newIndex);
        });
    }

    private void OnLeather2ColorBrowse(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_itemViewModel == null) return;
        OpenColorPicker(PaletteColorService.Palettes.Leather2, _itemViewModel.Leather2Color, "Select Leather 2 Color", newIndex =>
        {
            _itemViewModel.Leather2Color = newIndex;
            UpdateColorSwatch(Leather2ColorSwatch, PaletteColorService.Palettes.Leather2, newIndex);
        });
    }

    private void OnMetal1ColorBrowse(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_itemViewModel == null) return;
        OpenColorPicker(PaletteColorService.Palettes.Metal1, _itemViewModel.Metal1Color, "Select Metal 1 Color", newIndex =>
        {
            _itemViewModel.Metal1Color = newIndex;
            UpdateColorSwatch(Metal1ColorSwatch, PaletteColorService.Palettes.Metal1, newIndex);
        });
    }

    private void OnMetal2ColorBrowse(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_itemViewModel == null) return;
        OpenColorPicker(PaletteColorService.Palettes.Metal2, _itemViewModel.Metal2Color, "Select Metal 2 Color", newIndex =>
        {
            _itemViewModel.Metal2Color = newIndex;
            UpdateColorSwatch(Metal2ColorSwatch, PaletteColorService.Palettes.Metal2, newIndex);
        });
    }
}
