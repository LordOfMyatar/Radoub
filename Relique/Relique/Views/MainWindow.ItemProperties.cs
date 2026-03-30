using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ItemEditor.Services;
using Radoub.Formats.Uti;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemEditor.Views;

public partial class MainWindow
{
    // --- Item Properties UI ---

    private string? _selectedCategory;
    private Dictionary<string, string[]> _categoryKeywords = new();

    private void InitializePropertySearchHandler()
    {
        PropertySearchBox.TextChanged += OnPropertySearchTextChanged;
        InitializeCategoryFilter();
    }

    private void InitializeCategoryFilter()
    {
        CategoryFilterComboBox.Items.Clear();
        CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Categories", Tag = (string?)null });

        // Categories based on common item property groupings from nwscript.nss
        var categories = new (string Label, string[] Keywords)[]
        {
            ("Bonus/Enhancement", new[] { "Bonus", "Enhancement", "Mighty", "Keen" }),
            ("Damage", new[] { "Damage", "Massive Critical" }),
            ("Defense/AC", new[] { "AC", "Saving Throw", "Spell Resistance", "Immunity", "Damage Reduction", "Damage Resistance" }),
            ("On Hit", new[] { "On Hit", "On Monster" }),
            ("Cast Spell", new[] { "Cast Spell" }),
            ("Penalty/Decreased", new[] { "Decreased", "Vulnerability", "No Damage" }),
            ("Skill/Ability", new[] { "Skill", "Ability" }),
            ("Use Limitation", new[] { "Use Limitation" }),
            ("Miscellaneous", new[] { "Regeneration", "Haste", "Darkvision", "Light", "True Seeing", "Freedom", "Trap", "Poison", "Visual", "Weight", "Material", "Quality" }),
        };

        foreach (var (label, _) in categories)
        {
            CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = label, Tag = label });
        }

        _categoryKeywords = categories.ToDictionary(c => c.Label, c => c.Keywords);
        CategoryFilterComboBox.SelectedIndex = 0;
        CategoryFilterComboBox.SelectionChanged += OnCategoryFilterChanged;
    }

    private void PopulateAvailableProperties(string? searchFilter = null)
    {
        AvailablePropertiesTree.Items.Clear();
        _checkedPropertyIndices.Clear();
        UpdateAddCheckedButton();

        if (_itemPropertyService == null)
            return;

        var types = string.IsNullOrWhiteSpace(searchFilter)
            ? _itemPropertyService.GetAvailablePropertyTypes()
            : _itemPropertyService.SearchProperties(searchFilter);

        // Apply category filter
        if (_selectedCategory != null && _categoryKeywords.TryGetValue(_selectedCategory, out var keywords))
        {
            types = types.Where(t =>
                keywords.Any(k => t.DisplayName.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // Filter by base item type: only show properties valid for the current base item (#1972)
        if (_currentItem != null)
        {
            var validIndices = _itemPropertyService.GetValidPropertyIndicesForBaseItem(_currentItem.BaseItem);
            if (validIndices != null)
            {
                types = types.Where(t => validIndices.Contains(t.PropertyIndex)).ToList();
            }
        }

        // Move semantics: filter out properties already assigned to the item (#1809)
        var assignedProperties = _currentItem?.Properties ?? new List<ItemProperty>();
        types = types.Where(t => _itemPropertyService.HasAvailableSubtypes(t.PropertyIndex, assignedProperties)).ToList();

        PropertyCountLabel.Text = $"({types.Count})";

        foreach (var type in types)
        {
            var checkBox = new CheckBox
            {
                Content = type.DisplayName,
                Tag = type,
                Margin = new Thickness(0)
            };
            var capturedType = type;
            checkBox.IsCheckedChanged += (_, _) =>
            {
                if (checkBox.IsChecked == true)
                    _checkedPropertyIndices.Add(capturedType.PropertyIndex);
                else
                    _checkedPropertyIndices.Remove(capturedType.PropertyIndex);
                UpdateAddCheckedButton();
            };

            var node = new TreeViewItem
            {
                Header = checkBox,
                Tag = type
            };

            // Add subtypes as children if this property has them — only available ones
            var hasMatchingSubtype = false;
            if (type.HasSubtypes)
            {
                var allSubtypes = _itemPropertyService.GetSubtypes(type.PropertyIndex);
                var availableSubtypes = _itemPropertyService.GetAvailableSubtypes(
                    type.PropertyIndex, allSubtypes, assignedProperties);

                foreach (var subtype in availableSubtypes)
                {
                    var subtypeNode = new TreeViewItem
                    {
                        Header = subtype.DisplayName,
                        Tag = subtype
                    };

                    // Bold matching subtypes when search is active
                    if (!string.IsNullOrWhiteSpace(searchFilter) &&
                        subtype.DisplayName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        hasMatchingSubtype = true;
                        subtypeNode.FontWeight = Avalonia.Media.FontWeight.Bold;
                    }

                    node.Items.Add(subtypeNode);
                }
            }

            // Auto-expand when a subtype matched the search
            if (hasMatchingSubtype)
                node.IsExpanded = true;

            AvailablePropertiesTree.Items.Add(node);
        }
    }

    private void UpdateAddCheckedButton()
    {
        AddCheckedButton.IsEnabled = _checkedPropertyIndices.Count > 0 && _currentItem != null;
    }

    private void OnPropertySearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        PopulateAvailableProperties(PropertySearchBox.Text);
    }

    private void OnCategoryFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CategoryFilterComboBox.SelectedItem is ComboBoxItem item)
        {
            _selectedCategory = item.Tag as string;
            PopulateAvailableProperties(PropertySearchBox.Text);
        }
    }

    private void OnContextMenuAddToItem(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null)
            return;

        // Get the selected tree item
        PropertyTypeInfo? propertyType = null;
        int subtypeIndex = 0;

        if (AvailablePropertiesTree.SelectedItem is TreeViewItem selectedNode)
        {
            if (selectedNode.Tag is PropertyTypeInfo type)
            {
                propertyType = type;
            }
            else if (selectedNode.Tag is TwoDAEntry subtypeEntry &&
                     selectedNode.Parent is TreeViewItem parentNode &&
                     parentNode.Tag is PropertyTypeInfo parentType)
            {
                propertyType = parentType;
                subtypeIndex = subtypeEntry.Index;
            }
        }

        if (propertyType == null)
            return;

        // Add with default values (first cost value, specified or first subtype)
        int costValueIndex = 0;
        if (propertyType.HasCostTable)
        {
            var costValues = _itemPropertyService.GetCostValues(propertyType.PropertyIndex);
            if (costValues.Count > 0)
                costValueIndex = costValues[0].Index;
        }

        if (subtypeIndex == 0 && propertyType.HasSubtypes)
        {
            var allSubtypes = _itemPropertyService.GetSubtypes(propertyType.PropertyIndex);
            var availableSubtypes = _itemPropertyService.GetAvailableSubtypes(
                propertyType.PropertyIndex, allSubtypes, _currentItem.Properties);
            if (availableSubtypes.Count > 0)
                subtypeIndex = availableSubtypes[0].Index;
            else
                return; // All subtypes assigned
        }

        var property = _itemPropertyService.CreateItemProperty(
            propertyType.PropertyIndex,
            subtypeIndex,
            costValueIndex,
            paramValueIndex: null);

        _currentItem.Properties.Add(property);
        RefreshAssignedProperties();
        MarkDirty();

        UpdateStatus($"Added property: {propertyType.DisplayName}");
    }

    private void OnAvailablePropertySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Selecting from Available tree exits edit mode
        _editingPropertyIndex = -1;
        ApplyEditButton.IsVisible = false;

        if (AvailablePropertiesTree.SelectedItem is TreeViewItem selectedNode)
        {
            PropertyTypeInfo? propertyType = null;

            if (selectedNode.Tag is PropertyTypeInfo type)
            {
                propertyType = type;
            }
            else if (selectedNode.Tag is TwoDAEntry && selectedNode.Parent is TreeViewItem parentNode && parentNode.Tag is PropertyTypeInfo parentType)
            {
                propertyType = parentType;
            }

            if (propertyType != null)
            {
                _selectedPropertyType = propertyType;
                UpdatePropertyConfigPanel(propertyType);
                AddPropertyButton.IsEnabled = _currentItem != null;

                // If a subtype child node was selected, pre-select it in the dropdown
                if (selectedNode.Tag is TwoDAEntry subtypeEntry && SubtypeComboBox.IsVisible)
                {
                    for (int i = 0; i < SubtypeComboBox.Items.Count; i++)
                    {
                        if (SubtypeComboBox.Items[i] is ComboBoxItem item && item.Tag is int idx && idx == subtypeEntry.Index)
                        {
                            SubtypeComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            _selectedPropertyType = null;
            PropertyConfigPanel.IsVisible = false;
            AddPropertyButton.IsEnabled = false;
        }
    }

    private void UpdatePropertyConfigPanel(PropertyTypeInfo propertyType)
    {
        PropertyConfigPanel.IsVisible = true;
        SelectedPropertyName.Text = propertyType.DisplayName;

        // Subtypes — only show available (unassigned) subtypes (#1809)
        // When editing, exclude the property being edited so its subtype stays selectable
        bool hasSubtypes = propertyType.HasSubtypes;
        SubtypeLabel.IsVisible = hasSubtypes;
        SubtypeComboBox.IsVisible = hasSubtypes;
        SubtypeComboBox.Items.Clear();
        if (hasSubtypes && _itemPropertyService != null)
        {
            var allSubtypes = _itemPropertyService.GetSubtypes(propertyType.PropertyIndex);
            var assignedProperties = _currentItem?.Properties ?? new List<ItemProperty>();

            // When editing, exclude the property at _editingPropertyIndex from the filter
            if (_editingPropertyIndex >= 0 && _editingPropertyIndex < assignedProperties.Count)
            {
                var filtered = new List<ItemProperty>(assignedProperties);
                filtered.RemoveAt(_editingPropertyIndex);
                assignedProperties = filtered;
            }

            var availableSubtypes = _itemPropertyService.GetAvailableSubtypes(
                propertyType.PropertyIndex, allSubtypes, assignedProperties);

            foreach (var sub in availableSubtypes)
            {
                SubtypeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = sub.DisplayName,
                    Tag = sub.Index
                });
            }
            if (SubtypeComboBox.Items.Count > 0)
                SubtypeComboBox.SelectedIndex = 0;
        }

        // Cost values
        bool hasCost = propertyType.HasCostTable;
        CostValueLabel.IsVisible = hasCost;
        CostValueComboBox.IsVisible = hasCost;
        CostValueComboBox.Items.Clear();
        if (hasCost && _itemPropertyService != null)
        {
            var costValues = _itemPropertyService.GetCostValues(propertyType.PropertyIndex);
            foreach (var cost in costValues)
            {
                CostValueComboBox.Items.Add(new ComboBoxItem
                {
                    Content = cost.DisplayName,
                    Tag = cost.Index
                });
            }
            if (CostValueComboBox.Items.Count > 0)
                CostValueComboBox.SelectedIndex = 0;
        }

        // Param values
        bool hasParam = propertyType.HasParamTable;
        ParamValueLabel.IsVisible = hasParam;
        ParamValueComboBox.IsVisible = hasParam;
        ParamValueComboBox.Items.Clear();
        if (hasParam && _itemPropertyService != null)
        {
            var paramValues = _itemPropertyService.GetParamValues(propertyType.PropertyIndex);
            foreach (var param in paramValues)
            {
                ParamValueComboBox.Items.Add(new ComboBoxItem
                {
                    Content = param.DisplayName,
                    Tag = param.Index
                });
            }
            if (ParamValueComboBox.Items.Count > 0)
                ParamValueComboBox.SelectedIndex = 0;
        }
    }

    private void OnAddPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null || _selectedPropertyType == null)
            return;

        int subtypeIndex = 0;
        if (SubtypeComboBox.IsVisible && SubtypeComboBox.SelectedItem is ComboBoxItem subItem && subItem.Tag is int subIdx)
            subtypeIndex = subIdx;

        int costValueIndex = 0;
        if (CostValueComboBox.IsVisible && CostValueComboBox.SelectedItem is ComboBoxItem costItem && costItem.Tag is int costIdx)
            costValueIndex = costIdx;

        int? paramValueIndex = null;
        if (ParamValueComboBox.IsVisible && ParamValueComboBox.SelectedItem is ComboBoxItem paramItem && paramItem.Tag is int paramIdx)
            paramValueIndex = paramIdx;

        var property = _itemPropertyService.CreateItemProperty(
            _selectedPropertyType.PropertyIndex,
            subtypeIndex,
            costValueIndex,
            paramValueIndex);

        _currentItem.Properties.Add(property);
        RefreshAssignedProperties();
        MarkDirty();

        UpdateStatus($"Added property: {_selectedPropertyType.DisplayName}");
    }

    private void OnAddCheckedClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null || _checkedPropertyIndices.Count == 0)
            return;

        var types = _itemPropertyService.GetAvailablePropertyTypes();
        int added = 0;

        foreach (var propIndex in _checkedPropertyIndices)
        {
            var type = types.FirstOrDefault(t => t.PropertyIndex == propIndex);
            if (type == null) continue;

            // Use first available subtype (move semantics)
            int subtypeIndex = 0;
            if (type.HasSubtypes)
            {
                var allSubtypes = _itemPropertyService.GetSubtypes(propIndex);
                var available = _itemPropertyService.GetAvailableSubtypes(
                    propIndex, allSubtypes, _currentItem.Properties);
                if (available.Count > 0)
                    subtypeIndex = available[0].Index;
                else
                    continue; // All subtypes assigned, skip
            }

            var property = _itemPropertyService.CreateItemProperty(propIndex, subtypeIndex, 0, null);
            _currentItem.Properties.Add(property);
            added++;
        }

        if (added > 0)
        {
            RefreshAssignedProperties();
            MarkDirty();
            UpdateStatus($"Added {added} properties");
        }

        // Uncheck all after adding
        foreach (var item in AvailablePropertiesTree.Items)
        {
            if (item is TreeViewItem node && node.Header is CheckBox cb)
                cb.IsChecked = false;
        }
    }

    private void OnRemovePropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null)
            return;

        var selectedIndices = AssignedPropertiesList.Selection.SelectedIndexes
            .Where(i => i >= 0 && i < _currentItem.Properties.Count)
            .OrderByDescending(i => i)
            .ToList();

        if (selectedIndices.Count == 0)
            return;

        foreach (var index in selectedIndices)
        {
            _currentItem.Properties.RemoveAt(index);
        }

        RefreshAssignedProperties();
        MarkDirty();

        var count = selectedIndices.Count;
        UpdateStatus(count == 1 ? "Property removed" : $"{count} properties removed");
    }

    private void OnClearAllPropertiesClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _currentItem.Properties.Count == 0)
            return;

        var count = _currentItem.Properties.Count;
        _currentItem.Properties.Clear();
        RefreshAssignedProperties();
        MarkDirty();
        UpdateStatus($"Cleared {count} properties");
    }

    private void OnAssignedPropertySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedCount = AssignedPropertiesList.Selection.SelectedIndexes.Count();
        bool hasSelection = selectedCount > 0;
        RemovePropertyButton.IsEnabled = hasSelection;
        EditPropertyButton.IsEnabled = selectedCount == 1;
    }

    private void OnEditPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null || AssignedPropertiesList.SelectedIndex < 0)
            return;

        var index = AssignedPropertiesList.SelectedIndex;
        if (index >= _currentItem.Properties.Count)
            return;

        var prop = _currentItem.Properties[index];
        _editingPropertyIndex = index;

        var types = _itemPropertyService.GetAvailablePropertyTypes();
        var type = types.FirstOrDefault(t => t.PropertyIndex == prop.PropertyName);
        if (type == null)
        {
            UpdateStatus($"Unknown property type: {prop.PropertyName}");
            return;
        }

        _selectedPropertyType = type;
        UpdatePropertyConfigPanel(type);

        // Pre-select current values in dropdowns
        SelectComboBoxByTag(SubtypeComboBox, prop.Subtype);
        SelectComboBoxByTag(CostValueComboBox, prop.CostValue);
        if (prop.Param1 != 0xFF)
            SelectComboBoxByTag(ParamValueComboBox, prop.Param1Value);

        ApplyEditButton.IsVisible = true;
        AddPropertyButton.IsEnabled = false;

        UpdateStatus($"Editing: {type.DisplayName}");
    }

    private void OnApplyEditClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null || _selectedPropertyType == null)
            return;

        if (_editingPropertyIndex < 0 || _editingPropertyIndex >= _currentItem.Properties.Count)
            return;

        int subtypeIndex = 0;
        if (SubtypeComboBox.IsVisible && SubtypeComboBox.SelectedItem is ComboBoxItem subItem && subItem.Tag is int subIdx)
            subtypeIndex = subIdx;

        int costValueIndex = 0;
        if (CostValueComboBox.IsVisible && CostValueComboBox.SelectedItem is ComboBoxItem costItem && costItem.Tag is int costIdx)
            costValueIndex = costIdx;

        int? paramValueIndex = null;
        if (ParamValueComboBox.IsVisible && ParamValueComboBox.SelectedItem is ComboBoxItem paramItem && paramItem.Tag is int paramIdx)
            paramValueIndex = paramIdx;

        var property = _itemPropertyService.CreateItemProperty(
            _selectedPropertyType.PropertyIndex,
            subtypeIndex,
            costValueIndex,
            paramValueIndex);

        _currentItem.Properties[_editingPropertyIndex] = property;
        RefreshAssignedProperties();
        MarkDirty();

        _editingPropertyIndex = -1;
        ApplyEditButton.IsVisible = false;
        PropertyConfigPanel.IsVisible = false;

        UpdateStatus($"Updated property: {_selectedPropertyType.DisplayName}");
    }

    private static void SelectComboBoxByTag(ComboBox comboBox, int tagValue)
    {
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item && item.Tag is int idx && idx == tagValue)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }
    }

    private void RefreshAssignedProperties()
    {
        AssignedPropertiesList.Items.Clear();

        if (_currentItem == null)
            return;

        foreach (var prop in _currentItem.Properties)
        {
            var displayText = ResolvePropertyDisplayText(prop);
            AssignedPropertiesList.Items.Add(new ListBoxItem
            {
                Content = displayText,
                Tag = prop
            });
        }

        RemovePropertyButton.IsEnabled = false;
        EditPropertyButton.IsEnabled = false;
        ClearAllPropertiesButton.IsEnabled = _currentItem.Properties.Count > 0;

        // Refresh available properties list to reflect move semantics (#1809)
        PopulateAvailableProperties(PropertySearchBox.Text);

        RefreshStatistics();
    }

    private string ResolvePropertyDisplayText(ItemProperty prop)
    {
        if (_itemPropertyService == null)
            return $"Property {prop.PropertyName} (Sub:{prop.Subtype} Cost:{prop.CostValue})";

        var types = _itemPropertyService.GetAvailablePropertyTypes();
        var type = types.FirstOrDefault(t => t.PropertyIndex == prop.PropertyName);
        var name = type?.DisplayName ?? $"Property {prop.PropertyName}";

        var parts = new List<string> { name };

        if (type?.HasSubtypes == true)
        {
            var subtypes = _itemPropertyService.GetSubtypes(prop.PropertyName);
            var subtype = subtypes.FirstOrDefault(s => s.Index == prop.Subtype);
            if (subtype != null)
                parts.Add(subtype.DisplayName);
        }

        if (type?.HasCostTable == true)
        {
            var costValues = _itemPropertyService.GetCostValues(prop.PropertyName);
            var cost = costValues.FirstOrDefault(c => c.Index == prop.CostValue);
            if (cost != null)
                parts.Add(cost.DisplayName);
        }

        if (type?.HasParamTable == true && prop.Param1 != 0xFF)
        {
            var paramValues = _itemPropertyService.GetParamValues(prop.PropertyName);
            var param = paramValues.FirstOrDefault(p => p.Index == prop.Param1Value);
            if (param != null)
                parts.Add(param.DisplayName);
        }

        return string.Join(" ", parts);
    }

    private void RefreshStatistics()
    {
        if (_currentItem == null || _itemStatisticsService == null)
        {
            ItemStatisticsPanel.IsVisible = false;
            return;
        }

        var stats = _itemStatisticsService.GenerateStatistics(_currentItem.Properties);
        ItemStatisticsText.Text = stats;
        ItemStatisticsPanel.IsVisible = true;
    }
}
