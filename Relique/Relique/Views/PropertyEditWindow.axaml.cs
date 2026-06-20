using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ItemEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Uti;

namespace ItemEditor.Views;

/// <summary>
/// Modal property configuration popup (#2406). Replaces the disconnected bottom PropertyConfigPanel:
/// scopes the edit to one property, gives searchable Subtype/Value/Param lists (long spell lists),
/// and shows a live preview. The model is NOT mutated here — OK returns a built
/// <see cref="ItemProperty"/> via <c>ShowDialog&lt;ItemProperty?&gt;</c>; Cancel returns null.
///
/// Opened in two modes: add (configured single add of a tree-selected property type) and edit
/// (double-click / Edit on an assigned property, pre-selecting its current values).
/// </summary>
public partial class PropertyEditWindow : Window
{
    private readonly ItemPropertyService _service;
    private readonly PropertyTypeInfo _type;

    private readonly List<TwoDAEntry> _subtypes;
    private readonly List<TwoDAEntry> _costs;
    private readonly List<TwoDAEntry> _params;

    /// <summary>Designer-only ctor.</summary>
    public PropertyEditWindow()
    {
        InitializeComponent();
        _service = null!;
        _type = null!;
        _subtypes = new();
        _costs = new();
        _params = new();
    }

    /// <param name="service">Property service for 2DA/TLK lookups and availability filtering.</param>
    /// <param name="type">The property type being added/edited (fixed in the dialog).</param>
    /// <param name="assignedProperties">Current item properties, for move-semantics subtype filtering.</param>
    /// <param name="editingProperty">The property being edited, or null for a new add.</param>
    /// <param name="editingIndex">Index of the edited property (excluded from subtype filtering), or -1.</param>
    public PropertyEditWindow(
        ItemPropertyService service,
        PropertyTypeInfo type,
        IReadOnlyList<ItemProperty> assignedProperties,
        ItemProperty? editingProperty,
        int editingIndex,
        int? preselectSubtype = null)
    {
        InitializeComponent();
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _type = type ?? throw new ArgumentNullException(nameof(type));

        Title = editingProperty != null ? "Edit Property" : "Configure Property";
        PropertyNameText.Text = type.DisplayName;

        // Subtypes: only available (unassigned) ones; when editing, keep the edited property's own
        // subtype selectable by excluding it from the filter.
        _subtypes = new();
        if (type.HasSubtypes)
        {
            var all = service.GetSubtypes(type.PropertyIndex);
            var assignedForFilter = assignedProperties.ToList();
            if (editingIndex >= 0 && editingIndex < assignedForFilter.Count)
                assignedForFilter.RemoveAt(editingIndex);
            _subtypes = service.GetAvailableSubtypes(type.PropertyIndex, all, assignedForFilter);
        }
        _costs = type.HasCostTable ? service.GetCostValues(type.PropertyIndex) : new();
        _params = type.HasParamTable ? service.GetParamValues(type.PropertyIndex) : new();

        ConfigureBox(SubtypeBox, SubtypeRow, _subtypes);
        ConfigureBox(CostBox, CostRow, _costs);
        ConfigureBox(ParamBox, ParamRow, _params);

        // Pre-select: current values when editing, the tree-clicked subtype when adding from a
        // subtype node, else the first entry of each list.
        PreSelect(SubtypeBox, _subtypes, editingProperty?.Subtype ?? preselectSubtype);
        PreSelect(CostBox, _costs, editingProperty?.CostValue);
        PreSelect(ParamBox, _params,
            editingProperty != null && editingProperty.Param1 != 0xFF ? editingProperty.Param1Value : (int?)null);

        SubtypeBox.SelectionChanged += (_, _) => UpdatePreview();
        CostBox.SelectionChanged += (_, _) => UpdatePreview();
        ParamBox.SelectionChanged += (_, _) => UpdatePreview();

        UpdatePreview();
        Opened += (_, _) =>
        {
            if (SubtypeRow.IsVisible) SubtypeBox.Focus();
            else OkButton.Focus();
        };
    }

    private static void ConfigureBox(AutoCompleteBox box, Grid row, List<TwoDAEntry> items)
    {
        if (items.Count == 0)
        {
            row.IsVisible = false;
            return;
        }
        box.ItemsSource = items;
        // AutoCompleteBox filters/displays on the string; TwoDAEntry.ToString is its DisplayName.
        box.ItemFilter = (search, item) =>
            string.IsNullOrEmpty(search) ||
            (item is TwoDAEntry e && e.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static void PreSelect(AutoCompleteBox box, List<TwoDAEntry> items, int? index)
    {
        if (items.Count == 0) return;
        var match = index.HasValue ? items.FirstOrDefault(e => e.Index == index.Value) : null;
        box.SelectedItem = match ?? items[0];
        box.Text = (match ?? items[0]).DisplayName;
    }

    private static TwoDAEntry? Selected(AutoCompleteBox box, List<TwoDAEntry> items)
    {
        if (items.Count == 0) return null;
        if (box.SelectedItem is TwoDAEntry e) return e;
        // Fall back to a text match so a typed-but-not-clicked entry still resolves.
        return items.FirstOrDefault(i =>
            string.Equals(i.DisplayName, box.Text, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdatePreview()
    {
        var parts = new List<string> { _type.DisplayName };
        var sub = Selected(SubtypeBox, _subtypes);
        if (sub != null) parts.Add(sub.DisplayName);
        var cost = Selected(CostBox, _costs);
        if (cost != null) parts.Add(cost.DisplayName);
        var prm = Selected(ParamBox, _params);
        if (prm != null) parts.Add(prm.DisplayName);
        PreviewText.Text = string.Join(" ", parts);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        // A property with subtypes must have one chosen.
        int subtypeIndex = 0;
        if (_subtypes.Count > 0)
        {
            var sub = Selected(SubtypeBox, _subtypes);
            if (sub == null)
            {
                ShowMessage("Choose a subtype.");
                return;
            }
            subtypeIndex = sub.Index;
        }

        int costValueIndex = Selected(CostBox, _costs)?.Index ?? 0;
        int? paramValueIndex = _params.Count > 0 ? Selected(ParamBox, _params)?.Index : null;

        try
        {
            var property = _service.CreateItemProperty(
                _type.PropertyIndex, subtypeIndex, costValueIndex, paramValueIndex);
            Close(property);
        }
        catch (Exception ex)
        {
            // Keep the dialog open on a bad combo (#2166 spirit) and surface the reason.
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"PropertyEditWindow failed to build {_type.DisplayName}: {ex.GetType().Name}: {ex.Message}");
            ShowMessage($"Cannot build property: {ex.Message}");
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void ShowMessage(string text)
    {
        MessageText.Text = text;
        MessageText.IsVisible = true;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
