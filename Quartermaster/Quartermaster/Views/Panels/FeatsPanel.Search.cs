using System;
using System.Linq;
using Quartermaster.Services;

namespace Quartermaster.Views.Panels;

/// <summary>
/// FeatsPanel partial class - Search and filter functionality.
/// </summary>
public partial class FeatsPanel
{
    /// <summary>
    /// Applies search text and filter checkboxes to the feat list.
    /// </summary>
    private void ApplySearchAndFilter()
    {
        if (_allFeats.Count == 0)
            return;

        var filtered = _allFeats.AsEnumerable();

        // Apply search filter
        var searchText = _searchTextBox?.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(searchText))
        {
            filtered = filtered.Where(f =>
                f.FeatName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply category filter (ComboBox)
        int categoryIndex = _categoryFilterComboBox?.SelectedIndex ?? 0;
        filtered = categoryIndex switch
        {
            0 => filtered, // All Categories
            1 => filtered.Where(f => f.Category == FeatCategory.Combat),
            2 => filtered.Where(f => f.Category == FeatCategory.ActiveCombat),
            3 => filtered.Where(f => f.Category == FeatCategory.Defensive),
            4 => filtered.Where(f => f.Category == FeatCategory.Magical),
            5 => filtered.Where(f => f.Category == FeatCategory.ClassRacial),
            6 => filtered.Where(f => f.Category == FeatCategory.Other),
            _ => filtered
        };

        // Apply status filter (checkboxes - OR logic for enabled statuses)
        // Filter should match the StatusText shown to users exactly
        var showAssigned = _showAssignedCheckBox?.IsChecked ?? true;
        var showGranted = _showGrantedCheckBox?.IsChecked ?? true;
        var showAvailable = _showAvailableCheckBox?.IsChecked ?? true;
        var showPrereqsUnmet = _showPrereqsUnmetCheckBox?.IsChecked ?? true;
        var showUnavailable = _showUnavailableCheckBox?.IsChecked ?? false;

        // Filter by status - match the displayed StatusText exactly
        filtered = filtered.Where(f =>
        {
            return f.StatusText switch
            {
                "Granted" => showGranted,
                "Assigned" => showAssigned,
                "Unavailable" => showUnavailable,
                "Available" => showAvailable,
                "Prereqs Unmet" => showPrereqsUnmet,
                _ => false // Unknown status - don't show
            };
        });

        // Update display
        _displayedFeats.Clear();
        foreach (var feat in filtered)
        {
            _displayedFeats.Add(feat);
        }

        // Show "no feats" message if empty
        if (_noFeatsText != null)
            _noFeatsText.IsVisible = _displayedFeats.Count == 0;

        // Update summary with filter info
        UpdateSummary();
    }
}
