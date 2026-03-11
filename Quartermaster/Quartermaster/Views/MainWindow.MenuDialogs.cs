using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Quartermaster.Views.Dialogs;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using DialogHelper = Quartermaster.Views.Helpers.DialogHelper;

namespace Quartermaster.Views;

/// <summary>
/// Menu handlers for dialogs: Settings, About, Level Up, Re-Level, Down-Level.
/// </summary>
public partial class MainWindow
{
    #region Menu Handlers - Dialogs

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.SetMainWindow(this);
        settingsWindow.Show(this);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        DialogHelper.ShowAboutDialog(this);
    }

    private void OnLevelUpClick(object? sender, RoutedEventArgs e) => LaunchLevelUpWizard();

    /// <summary>
    /// Launches the Level Up Wizard for the current creature.
    /// Called from menu (Ctrl+L), ClassesPanel Level Up button, and Add Class button.
    /// </summary>
    private async void LaunchLevelUpWizard()
    {
        if (_currentCreature == null)
            return;

        var wizard = new LevelUpWizardWindow(DisplayService, _currentCreature, _isBicFile);
        await wizard.ShowDialog(this);

        if (wizard.Confirmed)
        {
            // Refresh all panels to show updated data
            MarkDirty();
            LoadAllPanels(_currentCreature);
            UpdateCharacterHeader();
            UpdateStatus("Character leveled up");
        }
    }

    private void OnViewLevelHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (_currentCreature == null)
            return;

        var history = LevelHistoryService.Decode(_currentCreature.Comment);
        if (history == null || history.Count == 0)
        {
            DialogHelper.ShowMessageDialog(this, "Level History", "No level history recorded for this character.\n\nLevel history is recorded when you level up using Quartermaster's Level Up wizard.");
            return;
        }

        var formatted = LevelHistoryService.FormatForDisplay(
            history,
            DisplayService.GetClassName,
            DisplayService.GetFeatName,
            DisplayService.GetSkillName);

        DialogHelper.ShowMessageDialog(this, $"Level History ({history.Count} levels)", formatted);
    }

    private async void OnReLevelClick(object? sender, RoutedEventArgs e)
    {
        if (_currentCreature == null)
            return;

        int totalLevel = _currentCreature.ClassList.Sum(c => c.ClassLevel);
        if (totalLevel <= 1)
        {
            ShowErrorDialog("Cannot Re-Level", "Character is already at level 1.");
            return;
        }

        var firstClass = _currentCreature.ClassList.FirstOrDefault();
        var className = firstClass != null ? DisplayService.GetClassName(firstClass.Class) : "Unknown";

        var confirmed = await DialogHelper.ShowConfirmationDialog(
            this,
            "Re-Level Character",
            $"This will reset the character to level 1 {className}:\n\n" +
            $"• All class levels beyond 1 will be removed\n" +
            $"• All skills will be reset to 0\n" +
            $"• All choosable feats will be removed\n" +
            $"• Racial and class-granted feats will be kept\n\n" +
            $"After reset, use Level Up (Ctrl+L) to rebuild {totalLevel - 1} level(s).\n\n" +
            $"Continue?");

        if (!confirmed)
            return;

        // Strip character to level 1
        StripCharacterToLevelOne();

        MarkDirty();
        LoadAllPanels(_currentCreature);
        UpdateCharacterHeader();
        UpdateStatus($"Character reset to level 1. Use Level Up to rebuild.");
    }

    private void StripCharacterToLevelOne()
    {
        if (_currentCreature == null)
            return;

        StripCreatureToLevelOne(_currentCreature);
    }

    private async void OnDownLevelClick(object? sender, RoutedEventArgs e)
    {
        if (_currentCreature == null)
            return;

        int totalLevel = _currentCreature.ClassList.Sum(c => c.ClassLevel);
        if (totalLevel <= 1)
        {
            ShowErrorDialog("Cannot Down-Level", "Character is already at level 1.");
            return;
        }

        var firstClass = _currentCreature.ClassList.FirstOrDefault();
        var className = firstClass != null ? DisplayService.GetClassName(firstClass.Class) : "Unknown";
        var originalName = CreatureDisplayService.GetCreatureFullName(_currentCreature);

        var confirmed = await DialogHelper.ShowConfirmationDialog(
            this,
            "Down-Level Character",
            $"This will save a level 1 copy of \"{originalName}\" as a new file:\n\n" +
            $"• The copy will be level 1 {className}\n" +
            $"• All skills will be reset to 0\n" +
            $"• Only racial/class-granted feats will be kept\n" +
            $"• The original file will not be modified\n\n" +
            $"Choose where to save the level 1 copy.");

        if (!confirmed)
            return;

        // Show save dialog
        var filters = new List<Avalonia.Platform.Storage.FilePickerFileType>
        {
            new("NWN Creature") { Patterns = new[] { "*.utc" } },
            new("NWN Character") { Patterns = new[] { "*.bic" } },
            new("All Files") { Patterns = new[] { "*" } }
        };

        var suggestedName = $"{System.IO.Path.GetFileNameWithoutExtension(_currentFilePath ?? "creature")}_lvl1{System.IO.Path.GetExtension(_currentFilePath ?? ".utc")}";

        var storageProvider = StorageProvider;
        var result = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save Down-Leveled Copy",
            FileTypeChoices = filters,
            SuggestedFileName = suggestedName
        });

        if (result == null)
            return;

        var savePath = result.Path.LocalPath;

        try
        {
            // Create a deep copy (serialize and deserialize)
            var copy = CreateCreatureCopy(_currentCreature);

            // Strip the copy to level 1
            StripCreatureToLevelOne(copy);

            // Save the copy
            if (savePath.EndsWith(".bic", StringComparison.OrdinalIgnoreCase))
            {
                // BIC files wrap the UTC data in an additional layer
                // For simplicity, just save as UTC for now
                Radoub.Formats.Utc.UtcWriter.Write(copy, savePath);
            }
            else
            {
                Radoub.Formats.Utc.UtcWriter.Write(copy, savePath);
            }

            UpdateStatus($"Saved level 1 copy to {System.IO.Path.GetFileName(savePath)}");
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Save Failed", $"Failed to save level 1 copy: {ex.Message}");
        }
    }

    private UtcFile CreateCreatureCopy(UtcFile original)
    {
        // Create a copy by serializing and deserializing
        var buffer = Radoub.Formats.Utc.UtcWriter.Write(original);
        return Radoub.Formats.Utc.UtcReader.Read(buffer);
    }

    private void StripCreatureToLevelOne(UtcFile creature)
    {
        // Keep first class at level 1, remove others
        if (creature.ClassList.Count > 0)
        {
            var firstClass = creature.ClassList[0];
            firstClass.ClassLevel = 1;
            creature.ClassList.Clear();
            creature.ClassList.Add(firstClass);
        }

        // Get feats to keep (racial + class granted at level 1)
        var featsToKeep = new HashSet<ushort>();

        // Racial feats
        var racialFeats = DisplayService.Feats.GetRaceGrantedFeatIds(creature.Race);
        foreach (var f in racialFeats)
            featsToKeep.Add((ushort)f);

        // Class granted feats at level 1
        if (creature.ClassList.Count > 0)
        {
            var classGrantedFeats = DisplayService.Feats.GetClassGrantedFeatIds(creature.ClassList[0].Class);
            foreach (var f in classGrantedFeats)
                featsToKeep.Add((ushort)f);
        }

        // Filter feat list to only keep granted feats
        var newFeatList = creature.FeatList.Where(f => featsToKeep.Contains(f)).ToList();
        creature.FeatList.Clear();
        foreach (var f in newFeatList)
            creature.FeatList.Add(f);

        // Reset all skills to 0
        for (int i = 0; i < creature.SkillList.Count; i++)
            creature.SkillList[i] = 0;
    }

    private void ShowErrorDialog(string title, string message)
    {
        DialogHelper.ShowErrorDialog(this, title, message);
    }

    #endregion
}
