using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.UI.Services;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Views.Panels;

/// <summary>
/// FeatsPanel partial class - Summary display, assigned feats list, and theme helpers.
/// </summary>
public partial class FeatsPanel
{
    /// <summary>
    /// Updates the summary text showing feat counts and filter status.
    /// </summary>
    private void UpdateSummary()
    {
        var assignedCount = _assignedFeatIds.Count;
        var grantedCount = _grantedFeatIds.Count(g => _assignedFeatIds.Contains((ushort)g));
        var unavailableCount = _unavailableFeatIds.Count;
        var totalAvailable = _allFeats.Count;
        var displayedCount = _displayedFeats.Count;

        var filterNote = displayedCount < _allFeats.Count
            ? $" (showing {displayedCount} of {totalAvailable})"
            : "";

        // Calculate expected choosable feats (not including granted)
        var selectedCount = assignedCount - grantedCount; // Manually chosen feats
        string expectedNote = "";
        if (_currentCreature != null && _displayService != null)
        {
            var expectedInfo = _displayService.Feats.GetExpectedFeatCount(_currentCreature);
            int expected = expectedInfo.TotalExpected;
            int diff = selectedCount - expected;

            if (diff > 0)
                expectedNote = $" | Chosen: {selectedCount}/{expected} (+{diff})";
            else if (diff < 0)
                expectedNote = $" | Chosen: {selectedCount}/{expected} ({diff} available)";
            else
                expectedNote = $" | Chosen: {selectedCount}/{expected}";
        }

        SetText(_featsSummaryText,
            $"{assignedCount} assigned ({grantedCount} granted){expectedNote} | {unavailableCount} unavailable{filterNote}");

        // Update the left-side assigned feats list
        UpdateAssignedFeatsList();
    }

    /// <summary>
    /// Updates the assigned feats list panel on the left side.
    /// Shows feat names grouped by race, class (for granted feats), and a "Selected" section for manually assigned.
    /// </summary>
    private void UpdateAssignedFeatsList()
    {
        if (_assignedFeatsListPanel == null || _assignedFeatsListBorder == null) return;

        _assignedFeatsListPanel.Children.Clear();

        if (_currentCreature == null || _displayService == null || _assignedFeatIds.Count == 0)
        {
            _assignedFeatsListBorder.IsVisible = false;
            return;
        }

        // Get theme-aware font sizes
        var smallFontSize = this.FindResource("FontSizeSmall") as double? ?? 12;

        // Group feats by their source
        var racialFeats = new List<ushort>();
        var grantedByClass = new Dictionary<int, List<ushort>>(); // classId -> feat IDs
        var selectedFeats = new List<ushort>(); // manually selected (not granted by any class/race)

        foreach (var featId in _assignedFeatIds.OrderBy(f => GetFeatNameInternal(f)))
        {
            if (_grantedFeatIds.Contains(featId))
            {
                // Check if it's a racial feat first
                if (_displayService.IsFeatGrantedByRace(_currentCreature, featId))
                {
                    racialFeats.Add(featId);
                }
                else
                {
                    // Find which class grants this feat
                    var grantingClassId = _displayService.GetFeatGrantingClass(_currentCreature, featId);
                    if (grantingClassId >= 0)
                    {
                        if (!grantedByClass.ContainsKey(grantingClassId))
                            grantedByClass[grantingClassId] = new List<ushort>();
                        grantedByClass[grantingClassId].Add(featId);
                    }
                    else
                    {
                        // Granted but couldn't determine source - put in selected
                        selectedFeats.Add(featId);
                    }
                }
            }
            else
            {
                selectedFeats.Add(featId);
            }
        }

        bool hasAnyFeats = false;

        // Show racial feats first
        if (racialFeats.Count > 0)
        {
            hasAnyFeats = true;
            var raceName = _displayService.GetRaceName(_currentCreature.Race);

            // Race header
            var raceHeader = new TextBlock
            {
                Text = raceName,
                FontWeight = FontWeight.Bold,
                FontSize = smallFontSize,
                Foreground = BrushManager.GetInfoBrush(this),
                Margin = new Thickness(0, 0, 0, 6)
            };
            _assignedFeatsListPanel.Children.Add(raceHeader);

            // Racial feat names
            foreach (var featId in racialFeats)
            {
                var featName = GetFeatNameInternal(featId);
                var featText = new TextBlock
                {
                    Text = $"  {featName}",
                    FontSize = smallFontSize,
                    Foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? BrushManager.GetDisabledBrush(this),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                _assignedFeatsListPanel.Children.Add(featText);
            }
        }

        // Show class-granted feats
        foreach (var classEntry in _currentCreature.ClassList)
        {
            var classId = (int)classEntry.Class;
            if (!grantedByClass.ContainsKey(classId)) continue;

            var className = _displayService.GetClassName(classEntry.Class) ?? $"Class {classEntry.Class}";
            var featsForClass = grantedByClass[classId];

            hasAnyFeats = true;

            // Class header
            var classHeader = new TextBlock
            {
                Text = className,
                FontWeight = FontWeight.Bold,
                FontSize = smallFontSize,
                Foreground = BrushManager.GetWarningBrush(this),
                Margin = new Thickness(0, _assignedFeatsListPanel.Children.Count > 0 ? 12 : 0, 0, 6)
            };
            _assignedFeatsListPanel.Children.Add(classHeader);

            // Feat names
            foreach (var featId in featsForClass)
            {
                var featName = GetFeatNameInternal(featId);
                var featText = new TextBlock
                {
                    Text = $"  {featName}",
                    FontSize = smallFontSize,
                    Foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? BrushManager.GetDisabledBrush(this),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                _assignedFeatsListPanel.Children.Add(featText);
            }
        }

        // Show domain-granted feats (informational — engine auto-grants these)
        if (_currentCreature != null && _displayService != null)
        {
            var domainFeatsShown = new HashSet<int>();
            for (int i = 0; i < _currentCreature.ClassList.Count; i++)
            {
                var classEntry = _currentCreature.ClassList[i];
                if (!_displayService.ClassHasDomains(classEntry.Class)) continue;

                // Resolve domains: use Domain1/Domain2 if set, else infer from feats
                var (d1, d2) = _displayService.Domains.ResolveDomains(classEntry, _currentCreature.FeatList);
                var domainIds = new List<int>();
                if (d1 >= 0) domainIds.Add(d1);
                if (d2 >= 0) domainIds.Add(d2);

                foreach (var domainId in domainIds)
                {
                    var domainInfo = _displayService.Domains.GetDomainInfo(domainId);
                    if (domainInfo == null || domainInfo.GrantedFeatId < 0) continue;
                    if (!domainFeatsShown.Add(domainInfo.GrantedFeatId)) continue;

                    if (!hasAnyFeats || _assignedFeatsListPanel.Children.Count > 0)
                    {
                        // First domain feat — add section header
                        if (domainFeatsShown.Count == 1)
                        {
                            hasAnyFeats = true;
                            var domainHeader = new TextBlock
                            {
                                Text = "Domain",
                                FontWeight = FontWeight.Bold,
                                FontSize = smallFontSize,
                                Foreground = BrushManager.GetInfoBrush(this),
                                Margin = new Thickness(0, _assignedFeatsListPanel.Children.Count > 0 ? 12 : 0, 0, 6)
                            };
                            _assignedFeatsListPanel.Children.Add(domainHeader);
                        }
                    }

                    var featText = new TextBlock
                    {
                        Text = $"  {domainInfo.GrantedFeatName} ({domainInfo.Name})",
                        FontSize = smallFontSize,
                        Foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? BrushManager.GetDisabledBrush(this),
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    _assignedFeatsListPanel.Children.Add(featText);
                }
            }
        }

        // Show manually assigned feats (not granted by class/race)
        if (selectedFeats.Count > 0)
        {
            hasAnyFeats = true;

            // Assigned header (matches status column terminology)
            var selectedHeader = new TextBlock
            {
                Text = "Assigned",
                FontWeight = FontWeight.Bold,
                FontSize = smallFontSize,
                Foreground = BrushManager.GetSuccessBrush(this),
                Margin = new Thickness(0, _assignedFeatsListPanel.Children.Count > 0 ? 12 : 0, 0, 6)
            };
            _assignedFeatsListPanel.Children.Add(selectedHeader);

            // Feat names
            foreach (var featId in selectedFeats)
            {
                var featName = GetFeatNameInternal(featId);
                var featText = new TextBlock
                {
                    Text = $"  {featName}",
                    FontSize = smallFontSize,
                    Foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? BrushManager.GetDisabledBrush(this),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                _assignedFeatsListPanel.Children.Add(featText);
            }
        }

        _assignedFeatsListBorder.IsVisible = hasAnyFeats;
    }

    /// <summary>
    /// Clears the panel when no creature is loaded.
    /// </summary>
    public void ClearPanel()
    {
        _displayedFeats.Clear();
        _allFeats.Clear();
        _assignedFeatIds.Clear();
        _grantedFeatIds.Clear();
        _unavailableFeatIds.Clear();
        _abilities.Clear();
        _currentCreature = null;

        SetText(_featsSummaryText, "0 feats assigned");
        if (_noFeatsText != null)
            _noFeatsText.IsVisible = false;
        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = true;
        if (_searchTextBox != null)
            _searchTextBox.Text = "";
        if (_categoryFilterComboBox != null)
            _categoryFilterComboBox.SelectedIndex = 0;
        // Reset status checkboxes to defaults
        if (_showAssignedCheckBox != null)
            _showAssignedCheckBox.IsChecked = true;
        if (_showGrantedCheckBox != null)
            _showGrantedCheckBox.IsChecked = true;
        if (_showAvailableCheckBox != null)
            _showAvailableCheckBox.IsChecked = true;
        if (_showPrereqsUnmetCheckBox != null)
            _showPrereqsUnmetCheckBox.IsChecked = true;
        if (_showUnavailableCheckBox != null)
            _showUnavailableCheckBox.IsChecked = false;
        // Hide the assigned feats list panel
        if (_assignedFeatsListBorder != null)
            _assignedFeatsListBorder.IsVisible = false;
        if (_assignedFeatsListPanel != null)
            _assignedFeatsListPanel.Children.Clear();
    }

    #region Helper Methods

    private string GetFeatNameInternal(ushort featId)
    {
        if (_displayService != null)
            return _displayService.GetFeatName(featId);
        return $"Feat {featId}";
    }

    private string GetSpellNameInternal(ushort spellId)
    {
        if (_displayService != null)
            return _displayService.GetSpellName(spellId);
        return $"Spell {spellId}";
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }

    #endregion

    private static IBrush GetTransparentRowBackground(IBrush baseBrush, byte alpha = 30)
    {
        if (baseBrush is SolidColorBrush scb)
        {
            var c = scb.Color;
            return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        }
        return Brushes.Transparent;
    }
}
