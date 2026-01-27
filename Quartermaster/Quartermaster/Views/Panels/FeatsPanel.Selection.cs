using Quartermaster.ViewModels;
using System;

namespace Quartermaster.Views.Panels;

/// <summary>
/// FeatsPanel partial class - Feat selection, add/remove operations.
/// </summary>
public partial class FeatsPanel
{
    /// <summary>
    /// Handles checkbox state changes for feat assignment.
    /// </summary>
    private void OnFeatAssignedChanged(FeatListViewModel feat, bool isNowAssigned)
    {
        if (_isLoading || _currentCreature == null) return;

        if (isNowAssigned)
        {
            AddFeat(feat.FeatId);
        }
        else
        {
            RemoveFeat(feat.FeatId);
        }
    }

    /// <summary>
    /// Adds a feat to the creature's feat list.
    /// </summary>
    private void AddFeat(ushort featId)
    {
        if (_currentCreature == null)
            return;

        // Don't add if already assigned
        if (_currentCreature.FeatList.Contains(featId))
            return;

        // Add to creature's feat list
        _currentCreature.FeatList.Add(featId);
        _assignedFeatIds.Add(featId);

        // Refresh the display
        RefreshFeatDisplay(featId);

        // Notify listeners
        FeatsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes a feat from the creature's feat list.
    /// </summary>
    private void RemoveFeat(ushort featId)
    {
        if (_currentCreature == null)
            return;

        // Don't remove if not assigned
        if (!_currentCreature.FeatList.Contains(featId))
            return;

        // Don't remove granted feats
        if (_grantedFeatIds.Contains(featId))
            return;

        // Remove from creature's feat list
        _currentCreature.FeatList.Remove(featId);
        _assignedFeatIds.Remove(featId);

        // Refresh the display
        RefreshFeatDisplay(featId);

        // Notify listeners
        FeatsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Refreshes the display for a single feat after add/remove.
    /// </summary>
    private void RefreshFeatDisplay(ushort featId)
    {
        if (_currentCreature == null)
            return;

        // Find and update the feat in _allFeats
        var index = _allFeats.FindIndex(f => f.FeatId == featId);
        if (index >= 0)
        {
            _allFeats[index] = CreateFeatViewModel(featId, _currentCreature);
        }

        // Re-apply filter to update displayed list
        ApplySearchAndFilter();

        // Update summary
        UpdateSummary();
    }
}
