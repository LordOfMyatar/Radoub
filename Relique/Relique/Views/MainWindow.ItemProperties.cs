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

        // Auto-apply on combo change during edit mode (#2226). Suppressed when
        // _suppressAutoApply is true (programmatic repopulation / pre-select).
        SubtypeComboBox.SelectionChanged += OnEditComboSelectionChanged;
        CostValueComboBox.SelectionChanged += OnEditComboSelectionChanged;
        ParamValueComboBox.SelectionChanged += OnEditComboSelectionChanged;

        // Apply button is no longer needed in auto-apply mode (#2226). Hidden permanently
        // — kept in AXAML to avoid breaking the Click wireup, but never shown.
        ApplyEditButton.IsVisible = false;
    }

    private void OnEditComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!EditAutoApplyDecider.ShouldAutoApply(_editingPropertyIndex, _suppressAutoApply))
            return;

        ApplyEditCore(teardownOnSuccess: false);
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
    /// is something to add — either checked properties (bulk path) or a selected property
    /// (configured path).
    /// </summary>
    private void UpdateAddButtonState()
    {
        AddPropertyButton.IsEnabled = _currentItem != null
            && !_documentState.IsReadOnly
            && (_checkedProperties.Count > 0 || _selectedPropertyType != null);
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
                UpdateAddButtonState();

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
            UpdateAddButtonState();
        }
    }

    private void UpdatePropertyConfigPanel(PropertyTypeInfo propertyType)
    {
        // Suppress auto-apply while combos are being cleared/repopulated (#2226).
        // Each SelectedIndex assignment below fires SelectionChanged; without
        // suppression, opening an edit would auto-apply a half-built state.
        _suppressAutoApply = true;
        try
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
        finally
        {
            _suppressAutoApply = false;
        }
    }

    /// <summary>
    /// Single Add button (#2234). If any properties are checked in the tree, bulk-add
    /// them with default values; otherwise add the selected property using the configured
    /// values from the PropertyConfigPanel. One button, behavior follows the active state.
    /// </summary>
    private void OnAddPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null) return;
        if (_documentState.IsReadOnly) return;

        if (_checkedProperties.Count > 0)
            AddCheckedProperties();
        else
            AddConfiguredProperty();
    }

    private void AddConfiguredProperty()
    {
        if (_currentItem == null || _itemPropertyService == null || _selectedPropertyType == null)
            return;
        if (_documentState.IsReadOnly) return;

        int subtypeIndex = 0;
        if (SubtypeComboBox.IsVisible && SubtypeComboBox.SelectedItem is ComboBoxItem subItem && subItem.Tag is int subIdx)
            subtypeIndex = subIdx;

        int costValueIndex = 0;
        if (CostValueComboBox.IsVisible && CostValueComboBox.SelectedItem is ComboBoxItem costItem && costItem.Tag is int costIdx)
            costValueIndex = costIdx;

        int? paramValueIndex = null;
        if (ParamValueComboBox.IsVisible && ParamValueComboBox.SelectedItem is ComboBoxItem paramItem && paramItem.Tag is int paramIdx)
            paramValueIndex = paramIdx;

        TryAddProperty(_selectedPropertyType, subtypeIndex, costValueIndex, paramValueIndex);
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

    private void OnEditPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null || AssignedPropertiesList.SelectedIndex < 0)
            return;
        if (_documentState.IsReadOnly) return;

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

        // Pre-select current values without firing auto-apply (#2226).
        _suppressAutoApply = true;
        try
        {
            SelectComboBoxByTag(SubtypeComboBox, prop.Subtype);
            SelectComboBoxByTag(CostValueComboBox, prop.CostValue);
            if (prop.Param1 != 0xFF)
                SelectComboBoxByTag(ParamValueComboBox, prop.Param1Value);
        }
        finally
        {
            _suppressAutoApply = false;
        }

        // ApplyEditButton stays hidden under auto-apply mode (#2226).
        AddPropertyButton.IsEnabled = false;

        UpdateStatus($"Editing: {type.DisplayName} (auto-applies on change)");
    }

    private void OnApplyEditClick(object? sender, RoutedEventArgs e)
    {
        // Apply button hidden under auto-apply (#2226). Handler kept for AXAML wireup safety.
        ApplyEditCore(teardownOnSuccess: true);
    }

    /// <summary>
    /// Apply current PropertyConfigPanel ComboBox values to the property at _editingPropertyIndex (#2226).
    /// teardownOnSuccess=true mirrors the old explicit-Apply flow (closes the edit panel, exits edit mode);
    /// teardownOnSuccess=false is auto-apply mode (keeps panel open so user can keep tweaking).
    /// Rollback to oldProperty on failure preserves item state for bad combos (#2166 pattern).
    /// </summary>
    private void ApplyEditCore(bool teardownOnSuccess)
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

        var oldProperty = _currentItem.Properties[_editingPropertyIndex];
        var editingIndex = _editingPropertyIndex;

        try
        {
            var property = _itemPropertyService.CreateItemProperty(
                _selectedPropertyType.PropertyIndex,
                subtypeIndex,
                costValueIndex,
                paramValueIndex);

            _currentItem.Properties[editingIndex] = property;

            // RefreshAssignedProperties calls PopulateAvailableProperties which clears
            // the assigned list selection. In auto-apply mode we re-select the same
            // row + restore the edit state so the user can keep tweaking.
            RefreshAssignedProperties();
            MarkDirty();

            if (teardownOnSuccess)
            {
                _editingPropertyIndex = -1;
                ApplyEditButton.IsVisible = false;
                PropertyConfigPanel.IsVisible = false;
                UpdateStatus($"Updated property: {_selectedPropertyType.DisplayName}");
            }
            else
            {
                // Auto-apply: keep edit state intact, re-select the edited row.
                if (editingIndex < AssignedPropertiesList.Items.Count)
                    AssignedPropertiesList.SelectedIndex = editingIndex;
                _editingPropertyIndex = editingIndex;
                UpdateStatus($"Auto-applied: {_selectedPropertyType.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            // Roll back to previous value and report — preserves item state on bad combos (#2166).
            _currentItem.Properties[editingIndex] = oldProperty;
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to update {_selectedPropertyType.DisplayName}: {ex.GetType().Name}: {ex.Message}");
            UpdateStatus($"Cannot update {_selectedPropertyType.DisplayName}: {ex.Message}");
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

        try
        {
            var property = _itemPropertyService.CreateItemProperty(
                propertyType.PropertyIndex,
                subtypeIndex,
                costValueIndex,
                paramValueIndex);

            // Route through the undo manager. AddPropertyCommand wraps PropertyListMutator, so the
            // #2258 rollback-on-refresh-failure seam still runs; the manager refuses to record the
            // command (and so it never lands on the undo stack) if Do() self-rolled-back (#2231).
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
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to add {propertyType.DisplayName}: {ex.GetType().Name}: {ex.Message}");
            UpdateStatus($"Cannot add {propertyType.DisplayName}: {ex.Message}");
        }
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
