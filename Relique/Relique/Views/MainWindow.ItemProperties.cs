using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ItemEditor.Commands;
using ItemEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Uti;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemEditor.Views;

public partial class MainWindow
{
    // --- Item Properties UI ---

    private string? _selectedCategory;

    private void InitializePropertySearchHandler()
    {
        PropertySearchBox.TextChanged += OnPropertySearchTextChanged;
        InitializeCategoryFilter();
    }

    private void InitializeCategoryFilter()
    {
        CategoryFilterComboBox.Items.Clear();
        CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Categories", Tag = (string?)null });

        if (_itemPropertyService != null)
        {
            var availableProps = _itemPropertyService.GetAvailablePropertyTypes();
            foreach (var category in _categoryService.GetCategoryNames(availableProps))
            {
                CategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = category, Tag = category });
            }
        }

        CategoryFilterComboBox.SelectedIndex = 0;
        CategoryFilterComboBox.SelectionChanged += OnCategoryFilterChanged;
    }

    private void PopulateAvailableProperties(string? searchFilter = null)
    {
        // Capture expansion state so a refresh after Add doesn't collapse the user's open category (#2227).
        var expansionSnapshot = CaptureAvailablePropertiesExpansion();

        AvailablePropertiesTree.Items.Clear();
        _checkedProperties.Clear();
        UpdateAddButtonState();

        if (_itemPropertyService == null)
            return;

        var types = string.IsNullOrWhiteSpace(searchFilter)
            ? _itemPropertyService.GetAvailablePropertyTypes()
            : _itemPropertyService.SearchProperties(searchFilter);

        // Apply category filter
        if (_selectedCategory != null)
        {
            types = types.Where(t => _categoryService.IsInCategory(t, _selectedCategory)).ToList();
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
            // #2405: a property WITH subtypes is an expander-only parent (no checkbox) so the user
            // must tick the exact subtype — eliminating the silent "first child" add. A property
            // WITHOUT subtypes keeps a parent checkbox carrying the NoSubtype sentinel.
            var capturedType = type;
            TreeViewItem node;

            if (type.HasSubtypes)
            {
                node = new TreeViewItem
                {
                    Header = type.DisplayName,
                    Tag = type
                };
            }
            else
            {
                var checkBox = new CheckBox
                {
                    Content = type.DisplayName,
                    Tag = type,
                    Margin = new Thickness(0)
                };
                checkBox.IsCheckedChanged += (_, _) =>
                {
                    var pair = new CheckedProperty(capturedType.PropertyIndex, CheckedProperty.NoSubtype);
                    if (checkBox.IsChecked == true)
                        _checkedProperties.Add(pair);
                    else
                        _checkedProperties.Remove(pair);
                    UpdateAddButtonState();
                };
                node = new TreeViewItem
                {
                    Header = checkBox,
                    Tag = type
                };
            }

            // Add subtypes as checkable children if this property has them — only available ones
            var hasMatchingSubtype = false;
            if (type.HasSubtypes)
            {
                var allSubtypes = _itemPropertyService.GetSubtypes(type.PropertyIndex);
                var availableSubtypes = _itemPropertyService.GetAvailableSubtypes(
                    type.PropertyIndex, allSubtypes, assignedProperties);

                foreach (var subtype in availableSubtypes)
                {
                    var capturedSubtype = subtype;
                    var subtypeCheck = new CheckBox
                    {
                        Content = subtype.DisplayName,
                        Tag = subtype,
                        Margin = new Thickness(0)
                    };
                    subtypeCheck.IsCheckedChanged += (_, _) =>
                    {
                        var pair = new CheckedProperty(capturedType.PropertyIndex, capturedSubtype.Index);
                        if (subtypeCheck.IsChecked == true)
                            _checkedProperties.Add(pair);
                        else
                            _checkedProperties.Remove(pair);
                        UpdateAddButtonState();
                    };

                    var subtypeNode = new TreeViewItem
                    {
                        Header = subtypeCheck,
                        Tag = subtype
                    };

                    // Bold matching subtypes when search is active
                    if (!string.IsNullOrWhiteSpace(searchFilter) &&
                        subtype.DisplayName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        hasMatchingSubtype = true;
                        subtypeCheck.FontWeight = Avalonia.Media.FontWeight.Bold;
                    }

                    node.Items.Add(subtypeNode);
                }
            }

            // Auto-expand when a subtype matched the search, or when the user had this category
            // open before the rebuild (#2227).
            if (hasMatchingSubtype || expansionSnapshot.ShouldExpand(type.PropertyIndex))
                node.IsExpanded = true;

            AvailablePropertiesTree.Items.Add(node);
        }
    }

    /// <summary>
    /// Snapshot which top-level Available Properties tree nodes are currently expanded,
    /// keyed by PropertyIndex. Caller restores via TreeExpansionSnapshot.ShouldExpand
    /// during the rebuild loop (#2227).
    /// </summary>
    private TreeExpansionSnapshot CaptureAvailablePropertiesExpansion()
    {
        var expanded = new List<int>();
        foreach (var obj in AvailablePropertiesTree.Items)
        {
            if (obj is TreeViewItem tvi && tvi.IsExpanded && tvi.Tag is PropertyTypeInfo pti)
            {
                expanded.Add(pti.PropertyIndex);
            }
        }
        return TreeExpansionTracker.Capture(expanded);
    }

    /// <summary>
    /// The single Add button (#2234) is enabled when the document is editable and there
    /// is something checked in the tree (bulk path). Single configured-add of a merely-selected
    /// property is the Configure… button (#2406), not Add — so a selection alone must NOT enable
    /// Add, or the click is a silent no-op.
    /// </summary>
    private void UpdateAddButtonState()
    {
        AddPropertyButton.IsEnabled = _currentItem != null
            && !_documentState.IsReadOnly
            && _checkedProperties.Count > 0;
    }

    /// <summary>
    /// Sync editor controls to the current document read-only state (#2106). Called
    /// after PopulateEditor and from LoadArchiveItemAsync — toggles the read-only
    /// banner and disables every control that mutates _currentItem.
    /// </summary>
    private void ApplyReadOnlyVisualState()
    {
        bool ro = _documentState.IsReadOnly;
        ReadOnlyBanner.IsVisible = ro;

        // Gate every control that writes to _currentItem. Buttons stay disabled
        // even with selection while read-only.
        if (ro)
        {
            AddPropertyButton.IsEnabled = false;
            ConfigurePropertyButton.IsEnabled = false;
            EditPropertyButton.IsEnabled = false;
            RemovePropertyButton.IsEnabled = false;
            ClearAllPropertiesButton.IsEnabled = false;
        }

        // Disable the available-property tree so checkbox ticks (which feed AddChecked)
        // can't accumulate _checkedProperties entries while previewing.
        AvailablePropertiesTree.IsEnabled = !ro;
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

        TryAddProperty(propertyType, subtypeIndex, costValueIndex, paramValueIndex: null);
    }

    private void OnAvailablePropertySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Track the selected property type for the Configure button + right-click default-add.
        // The subtype (if a child node was selected) is captured for the Configure popup pre-select.
        _selectedPropertyType = null;
        _selectedSubtypeIndex = null;

        if (AvailablePropertiesTree.SelectedItem is TreeViewItem selectedNode)
        {
            if (selectedNode.Tag is PropertyTypeInfo type)
            {
                _selectedPropertyType = type;
            }
            else if (selectedNode.Tag is TwoDAEntry subtypeEntry &&
                     selectedNode.Parent is TreeViewItem parentNode &&
                     parentNode.Tag is PropertyTypeInfo parentType)
            {
                _selectedPropertyType = parentType;
                _selectedSubtypeIndex = subtypeEntry.Index;
            }
        }

        UpdateAddButtonState();
        UpdateConfigureButtonState();
    }

    private void UpdateConfigureButtonState()
    {
        ConfigurePropertyButton.IsEnabled = _currentItem != null
            && !_documentState.IsReadOnly
            && _selectedPropertyType != null;
    }

    /// <summary>
    /// The Add button (#2234) bulk-adds the checked properties with default values (#2405). Precise
    /// single-add with a chosen subtype/value/param is the Configure… button + popup (#2406).
    /// </summary>
    private void OnAddPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null) return;
        if (_documentState.IsReadOnly) return;

        if (_checkedProperties.Count > 0)
            AddCheckedProperties();
    }

    /// <summary>
    /// Configure… button (#2406): open the modal popup to add the tree-selected property with a
    /// chosen subtype/value/param. Routes the result through TryAddConfiguredProperty so the #2166
    /// validation + rollback path still runs.
    /// </summary>
    private async void OnConfigurePropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null || _selectedPropertyType == null)
            return;
        if (_documentState.IsReadOnly) return;

        var type = _selectedPropertyType;
        try
        {
            var popup = new PropertyEditWindow(
                _itemPropertyService, type, _currentItem.Properties,
                editingProperty: null, editingIndex: -1, preselectSubtype: _selectedSubtypeIndex);

            var result = await popup.ShowDialog<ItemProperty?>(this);
            if (result == null) return;

            TryAddConfiguredProperty(type, result);
        }
        catch (Exception ex)
        {
            // async-void: keep an exception from crashing the app (see OpenEditPopup).
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Configure popup failed for {type.DisplayName}: {ex.GetType().Name}: {ex.Message}");
            UpdateStatus($"Cannot configure {type.DisplayName}: {ex.Message}");
        }
    }

    private void AddCheckedProperties()
    {
        if (_currentItem == null || _itemPropertyService == null || _checkedProperties.Count == 0)
            return;
        if (_documentState.IsReadOnly) return;

        // #2405: each ticked (property, subtype) pair resolves to one property at its exact subtype
        // (no "first child" default). CheckedPropertyResolver is the pure, unit-tested mapping.
        var resolved = CheckedPropertyResolver.Resolve(
            _checkedProperties, _itemPropertyService, _currentItem.BaseItem, _currentItem.Properties);
        int added = 0;
        var skipped = resolved.Skipped;
        var toAdd = resolved.ToAdd;

        // Batch-add as a single undo step. BatchAddPropertiesCommand wraps PropertyListMutator so
        // the #2258 rollback-on-refresh-failure seam runs; the manager refuses to record it (#2231)
        // if the refresh throws and the model self-rolls-back.
        if (toAdd.Count > 0)
        {
            var cmd = new BatchAddPropertiesCommand(
                _currentItem.Properties, toAdd, RefreshAssignedProperties,
                $"add {toAdd.Count} properties");
            _undo.Execute(cmd);

            if (cmd.WasApplied)
            {
                added = toAdd.Count;
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Refresh failed after batch-adding {toAdd.Count} properties; rolled back");
                UpdateStatus("Could not add properties (UI refresh failed)");
                try { RefreshAssignedProperties(); } catch { /* best-effort */ }
                return;
            }
        }

        if (skipped.Count > 0)
            UpdateStatus($"Added {added}; skipped {skipped.Count} ({string.Join(", ", skipped)})");
        else if (added > 0)
            UpdateStatus($"Added {added} properties");

        // Uncheck all after adding. A successful batch refreshes (rebuilds) the tree and clears
        // _checkedProperties; this also covers the all-skipped path where no refresh ran. Checkboxes
        // now live on subtype children too (#2405), so walk parents and children.
        UncheckAllAvailableProperties();
    }

    /// <summary>
    /// Clear every checkbox in the Available Properties tree (parent no-subtype checkboxes and
    /// subtype-child checkboxes, #2405) and the backing pair set.
    /// </summary>
    private void UncheckAllAvailableProperties()
    {
        foreach (var item in AvailablePropertiesTree.Items)
        {
            if (item is not TreeViewItem node) continue;
            if (node.Header is CheckBox parentCb)
                parentCb.IsChecked = false;
            foreach (var child in node.Items)
            {
                if (child is TreeViewItem childNode && childNode.Header is CheckBox childCb)
                    childCb.IsChecked = false;
            }
        }
        _checkedProperties.Clear();
        UpdateAddButtonState();
    }

    private void OnRemovePropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null)
            return;
        if (_documentState.IsReadOnly) return;

        var selectedIndices = AssignedPropertiesList.Selection.SelectedIndexes
            .Where(i => i >= 0 && i < _currentItem.Properties.Count)
            .OrderByDescending(i => i)
            .ToList();

        if (selectedIndices.Count == 0)
            return;

        var count = selectedIndices.Count;

        // Remove as a single undo step. RemovePropertyCommand wraps PropertyListMutator (re-inserts
        // removed entries if the refresh throws, #2258) and reports false so the manager refuses to
        // record it (#2231) when nothing was removed or the refresh rolled back.
        var cmd = new RemovePropertyCommand(
            _currentItem.Properties, selectedIndices, RefreshAssignedProperties,
            count == 1 ? "remove property" : $"remove {count} properties");
        _undo.Execute(cmd);

        if (!cmd.WasApplied)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Refresh failed after removing {count} properties; rolled back");
            UpdateStatus("Could not remove properties (UI refresh failed)");
            try { RefreshAssignedProperties(); } catch { /* best-effort */ }
            return;
        }

        UpdateStatus(count == 1 ? "Property removed" : $"{count} properties removed");
    }

    private void OnClearAllPropertiesClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _currentItem.Properties.Count == 0)
            return;
        if (_documentState.IsReadOnly) return;

        var count = _currentItem.Properties.Count;

        // Clear as a single undo step. ClearPropertiesCommand wraps PropertyListMutator (restores the
        // snapshot if the refresh throws, #2258) and reports false so the manager refuses to record
        // it (#2231) when the list was empty or the refresh rolled back.
        var cmd = new ClearPropertiesCommand(
            _currentItem.Properties, RefreshAssignedProperties, $"clear {count} properties");
        _undo.Execute(cmd);

        if (!cmd.WasApplied)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Refresh failed after clearing {count} properties; rolled back");
            UpdateStatus("Could not clear properties (UI refresh failed)");
            try { RefreshAssignedProperties(); } catch { /* best-effort */ }
            return;
        }

        UpdateStatus($"Cleared {count} properties");
    }

    private void OnAssignedPropertySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedCount = AssignedPropertiesList.Selection.SelectedIndexes.Count();
        bool hasSelection = selectedCount > 0;
        bool ro = _documentState.IsReadOnly;
        RemovePropertyButton.IsEnabled = hasSelection && !ro;
        EditPropertyButton.IsEnabled = selectedCount == 1 && !ro;
    }

    private void OnEditPropertyClick(object? sender, RoutedEventArgs e) => OpenEditPopup();

    private void OnAssignedPropertyDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        => OpenEditPopup();

    /// <summary>
    /// Open the modal PropertyEditWindow to edit the selected assigned property (#2406). The popup
    /// pre-selects the property's current values and returns the edited ItemProperty; ApplyEdit
    /// commits it through the #2166 rollback path.
    /// </summary>
    private async void OpenEditPopup()
    {
        if (_currentItem == null || _itemPropertyService == null || AssignedPropertiesList.SelectedIndex < 0)
            return;
        if (_documentState.IsReadOnly) return;

        var index = AssignedPropertiesList.SelectedIndex;
        if (index >= _currentItem.Properties.Count)
            return;

        var prop = _currentItem.Properties[index];
        var types = _itemPropertyService.GetAvailablePropertyTypes();
        var type = types.FirstOrDefault(t => t.PropertyIndex == prop.PropertyName);
        if (type == null)
        {
            UpdateStatus($"Unknown property type: {prop.PropertyName}");
            return;
        }

        try
        {
            var popup = new PropertyEditWindow(
                _itemPropertyService, type, _currentItem.Properties,
                editingProperty: prop, editingIndex: index);

            var result = await popup.ShowDialog<ItemProperty?>(this);
            if (result == null) return; // Cancel discards — model untouched.

            ApplyEdit(index, type, result);
        }
        catch (Exception ex)
        {
            // An async-void UI handler that opens a window must not let an exception escape to the
            // Avalonia sync context (that crashes the app). Surface it as a status message instead.
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Edit popup failed for {type.DisplayName}: {ex.GetType().Name}: {ex.Message}");
            UpdateStatus($"Cannot edit {type.DisplayName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Replace the property at <paramref name="index"/> with the popup's result, routing through
    /// PropertyListMutator.ReplaceAt so the refresh-failure rollback seam runs (#2258 / #2166).
    /// </summary>
    private void ApplyEdit(int index, PropertyTypeInfo type, ItemProperty replacement)
    {
        if (_currentItem == null || _itemPropertyService == null) return;

        // Defense in depth (#2166): the popup filters subtypes, but re-check base-item validity at
        // commit time so a stale popup state can't write an invalid combo. Mirrors the add paths.
        if (!_itemPropertyService.IsPropertyValidForBaseItem(type.PropertyIndex, _currentItem.BaseItem))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Refused edit of {type.DisplayName} on base item {_currentItem.BaseItem} (validation table)");
            UpdateStatus($"Cannot apply {type.DisplayName} to this item type");
            return;
        }

        bool applied = PropertyListMutator.ReplaceAt(
            _currentItem.Properties, index, replacement, RefreshAssignedProperties);

        if (applied)
        {
            MarkDirty();
            if (index < AssignedPropertiesList.Items.Count)
                AssignedPropertiesList.SelectedIndex = index;
            UpdateStatus($"Updated property: {type.DisplayName}");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Refresh failed after editing {type.DisplayName}; rolled back");
            UpdateStatus($"Cannot update {type.DisplayName} (UI refresh failed)");
            try { RefreshAssignedProperties(); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Add a property to the current item with crash-recovery and base-item validation (#2166).
    /// Runs the validation recheck, then guards the Add + UI refresh in a try/catch so an
    /// invalid base-item × property combo surfaces as a status-bar message instead of
    /// killing the process.
    /// </summary>
    private void TryAddProperty(PropertyTypeInfo propertyType, int subtypeIndex, int costValueIndex, int? paramValueIndex)
    {
        if (_currentItem == null || _itemPropertyService == null)
            return;

        // Defense layer 2: re-check validity at add-time even if the tree filter let it through.
        if (!_itemPropertyService.IsPropertyValidForBaseItem(propertyType.PropertyIndex, _currentItem.BaseItem))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Refused {propertyType.DisplayName} on base item {_currentItem.BaseItem} (validation table)");
            UpdateStatus($"Cannot add {propertyType.DisplayName} to this item type");
            return;
        }

        // Move-semantics recheck: SubtypeComboBox can hold stale entries after a successful add
        // until the user re-selects from the tree, letting the same (prop, subtype) pair through twice.
        if (!_itemPropertyService.IsPropertyAvailable(propertyType.PropertyIndex, subtypeIndex, _currentItem.Properties))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Refused duplicate {propertyType.DisplayName} (subtype {subtypeIndex})");
            UpdateStatus($"{propertyType.DisplayName} already assigned with that subtype");
            return;
        }

        ItemProperty property;
        try
        {
            property = _itemPropertyService.CreateItemProperty(
                propertyType.PropertyIndex,
                subtypeIndex,
                costValueIndex,
                paramValueIndex);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to add {propertyType.DisplayName}: {ex.GetType().Name}: {ex.Message}");
            UpdateStatus($"Cannot add {propertyType.DisplayName}: {ex.Message}");
            return;
        }

        ExecuteAdd(propertyType, property);
    }

    /// <summary>
    /// Add a pre-built property (from the Configure… popup, #2406) with the same base-item +
    /// move-semantics validation and undo/rollback path as <see cref="TryAddProperty"/>.
    /// </summary>
    private void TryAddConfiguredProperty(PropertyTypeInfo propertyType, ItemProperty property)
    {
        if (_currentItem == null || _itemPropertyService == null)
            return;

        if (!_itemPropertyService.IsPropertyValidForBaseItem(propertyType.PropertyIndex, _currentItem.BaseItem))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Refused {propertyType.DisplayName} on base item {_currentItem.BaseItem} (validation table)");
            UpdateStatus($"Cannot add {propertyType.DisplayName} to this item type");
            return;
        }

        if (!_itemPropertyService.IsPropertyAvailable(propertyType.PropertyIndex, property.Subtype, _currentItem.Properties))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Refused duplicate {propertyType.DisplayName} (subtype {property.Subtype})");
            UpdateStatus($"{propertyType.DisplayName} already assigned with that subtype");
            return;
        }

        ExecuteAdd(propertyType, property);
    }

    /// <summary>
    /// Route a validated property through the undo manager. AddPropertyCommand wraps
    /// PropertyListMutator, so the #2258 rollback-on-refresh-failure seam runs; the manager refuses
    /// to record the command if Do() self-rolled-back (#2231).
    /// </summary>
    private void ExecuteAdd(PropertyTypeInfo propertyType, ItemProperty property)
    {
        if (_currentItem == null) return;

        var cmd = new AddPropertyCommand(
            _currentItem.Properties, property, RefreshAssignedProperties,
            $"add {propertyType.DisplayName}");
        _undo.Execute(cmd);

        if (cmd.WasApplied)
        {
            UpdateStatus($"Added property: {propertyType.DisplayName}");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Refresh failed after adding {propertyType.DisplayName}; rolled back");
            UpdateStatus($"Cannot add {propertyType.DisplayName} (UI refresh failed)");
            try { RefreshAssignedProperties(); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Real refresh of the assigned-properties list + available tree + statistics. Reached through
    /// the <c>RefreshAssignedProperties</c> seam (#2380) so a test can force it to throw.
    /// </summary>
    private void RefreshAssignedPropertiesCore()
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
        ClearAllPropertiesButton.IsEnabled = _currentItem.Properties.Count > 0 && !_documentState.IsReadOnly;

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

        // For armor items, prepend the base AC derived from parts_chest[Torso].ACBONUS
        // so the Item Statistics panel shows the AC that comes from the armor itself,
        // separate from any AC Bonus item-properties listed below (#2164 followup).
        var baseAc = ResolveBaseArmorAc();
        if (baseAc != null)
        {
            stats = string.IsNullOrEmpty(stats)
                ? $"Base Armor Class: {baseAc}"
                : $"Base Armor Class: {baseAc}{System.Environment.NewLine}{stats}";
        }

        ItemStatisticsText.Text = stats;
        ItemStatisticsPanel.IsVisible = true;

        RecomputeCost();
    }

    /// <summary>
    /// Recompute the engine-derived item Cost (wiki Ch4 §4.4) and push it to the
    /// view model so the read-only Cost field reflects the value the game will use.
    /// Falls back to the stored value when game data is unavailable (#2235).
    /// </summary>
    private void RecomputeCost()
    {
        if (_itemViewModel == null || _currentItem == null || _itemCostCalculator == null)
            return;

        // Don't mutate read-only archive items — their Cost display stays as stored.
        if (_documentState.IsReadOnly)
            return;

        var computed = _itemCostCalculator.Calculate(_currentItem);
        if (computed.HasValue)
            _itemViewModel.Cost = computed.Value;
    }

    /// <summary>
    /// Look up the base AC from parts_chest.ACBONUS at the row indicated by
    /// ArmorPart_Torso. Returns null if not an armor item, no game data, or AC unset.
    /// Mirrors UpdateArmorClassDisplay's logic but returns a value instead of mutating UI.
    /// </summary>
    private string? ResolveBaseArmorAc()
    {
        if (_itemViewModel == null || _gameDataService == null || !_gameDataService.IsConfigured)
            return null;

        // Only armor (ModelType 3) has Torso parts → base AC.
        var typeInfo = _baseItemTypes?.FirstOrDefault(t => t.BaseItemIndex == _itemViewModel.BaseItem);
        if (typeInfo == null || !typeInfo.HasArmorParts) return null;

        var torsoPart = _itemViewModel.GetArmorPart("Torso");
        var acBonus = _gameDataService.Get2DAValue("parts_chest", torsoPart, "ACBONUS");

        if (string.IsNullOrEmpty(acBonus) || acBonus == "****") return null;
        return acBonus;
    }
}
