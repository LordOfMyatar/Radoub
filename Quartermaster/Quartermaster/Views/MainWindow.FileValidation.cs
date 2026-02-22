using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using Radoub.UI.Views;
using DialogHelper = Quartermaster.Views.Helpers.DialogHelper;

namespace Quartermaster.Views;

/// <summary>
/// File validation: Aurora filename constraints, BIC class validation, rename workflow.
/// </summary>
public partial class MainWindow
{
    #region File Validation

    /// <summary>
    /// Validates that the filename meets Aurora Engine constraints.
    /// Aurora Engine filenames must be: lowercase, max 16 characters (excluding extension),
    /// alphanumeric and underscore only.
    /// </summary>
    /// <returns>True if valid, false otherwise with error dialog shown.</returns>
    private Task<bool> ValidateAuroraFilename(string filePath)
    {
        var filename = Path.GetFileNameWithoutExtension(filePath);

        // Check length (max 16 characters)
        if (filename.Length > 16)
        {
            DialogHelper.ShowMessageDialog(this, "Invalid Filename",
                $"Filename is too long for Aurora Engine.\n\n" +
                $"Current: \"{filename}\" ({filename.Length} characters)\n" +
                $"Maximum: 16 characters\n\n" +
                "The Aurora Engine (Neverwinter Nights) cannot load files with names longer than 16 characters.");
            return Task.FromResult(false);
        }

        // Check for uppercase letters
        if (filename.Any(char.IsUpper))
        {
            DialogHelper.ShowMessageDialog(this, "Invalid Filename",
                $"Filename contains uppercase letters.\n\n" +
                $"Current: \"{filename}\"\n" +
                $"Suggested: \"{filename.ToLowerInvariant()}\"\n\n" +
                "Aurora Engine filenames should be lowercase for compatibility.");
            return Task.FromResult(false);
        }

        // Check for invalid characters (only alphanumeric and underscore allowed)
        var invalidChars = filename.Where(c => !char.IsLetterOrDigit(c) && c != '_').ToList();
        if (invalidChars.Count > 0)
        {
            var invalidStr = string.Join("", invalidChars.Distinct());
            DialogHelper.ShowMessageDialog(this, "Invalid Filename",
                $"Filename contains invalid characters.\n\n" +
                $"Current: \"{filename}\"\n" +
                $"Invalid characters: \"{invalidStr}\"\n\n" +
                "Aurora Engine filenames can only contain letters, numbers, and underscores.");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Validates that a creature has at least one playable class for BIC files.
    /// </summary>
    /// <returns>True if valid for BIC, false otherwise with error dialog shown.</returns>
    private Task<bool> ValidatePlayableClassForBic(UtcFile creature)
    {
        if (creature.ClassList == null || creature.ClassList.Count == 0)
        {
            DialogHelper.ShowMessageDialog(this, "Invalid Character",
                "Cannot save as player character (BIC): No classes defined.\n\n" +
                "Add at least one class to the creature before saving as a BIC file.");
            return Task.FromResult(false);
        }

        // Check if any class is a playable class (PlayerClass = 1 in classes.2da)
        var hasPlayableClass = creature.ClassList.Any(c =>
        {
            var playerClass = GameData.Get2DAValue("classes", c.Class, "PlayerClass");
            return playerClass == "1";
        });

        if (!hasPlayableClass)
        {
            // Get the class names for the error message
            var classNames = creature.ClassList
                .Select(c => DisplayService.GetClassName(c.Class))
                .ToList();
            var classList = string.Join(", ", classNames);

            DialogHelper.ShowMessageDialog(this, "Invalid Character Class",
                $"Cannot save as player character (BIC): No playable class found.\n\n" +
                $"Current class(es): {classList}\n\n" +
                "Player characters require at least one playable class (Fighter, Wizard, Cleric, etc.). " +
                "NPC-only classes like Commoner or Animal cannot be used for player characters.");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    #endregion

    #region Rename File

    /// <summary>
    /// Handles rename request from AdvancedPanel.
    /// </summary>
    private async void OnRenameRequested(object? sender, EventArgs e)
    {
        await RenameCurrentFileAsync();
    }

    /// <summary>
    /// Renames the current file using a safe save-rename-reload workflow.
    /// </summary>
    private async Task RenameCurrentFileAsync()
    {
        if (_currentCreature == null || string.IsNullOrEmpty(_currentFilePath))
        {
            UpdateStatus("No file loaded to rename");
            return;
        }

        // BIC files don't have ResRef, cannot be renamed this way
        if (_isBicFile)
        {
            DialogHelper.ShowMessageDialog(this, "Cannot Rename",
                "Player character (BIC) files cannot be renamed using this feature.\n\n" +
                "Use File > Save As instead.");
            return;
        }

        var directory = Path.GetDirectoryName(_currentFilePath) ?? "";
        var currentName = Path.GetFileNameWithoutExtension(_currentFilePath);
        var extension = Path.GetExtension(_currentFilePath);

        // Show rename dialog
        var newName = await RenameDialog.ShowAsync(this, currentName, directory, extension);
        if (string.IsNullOrEmpty(newName))
        {
            return; // User cancelled
        }

        // Check if file has unsaved changes
        if (_isDirty)
        {
            var result = await DialogHelper.ShowConfirmationDialog(this, "Save Changes",
                "The file has unsaved changes. Save before renaming?");

            if (!result)
            {
                return; // User cancelled
            }

            // Save current file
            await SaveFile();
        }

        var newFilePath = Path.GetFullPath(Path.Combine(directory, newName + extension));

        // Validate path stays within the original directory (prevent traversal)
        var resolvedDirectory = Path.GetFullPath(directory);
        if (!newFilePath.StartsWith(resolvedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Path traversal attempt detected in rename");
            DialogHelper.ShowMessageDialog(this, "Invalid Name",
                "The filename contains invalid path characters.");
            return;
        }

        try
        {
            // Rename file on disk
            File.Move(_currentFilePath, newFilePath);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Renamed file: {UnifiedLogger.SanitizePath(_currentFilePath)} -> {UnifiedLogger.SanitizePath(newFilePath)}");

            // Update internal ResRef to match new filename
            _currentCreature.TemplateResRef = newName;

            // Save file to persist the new ResRef
            _currentFilePath = newFilePath;
            await SaveFile();

            // Update UI
            AdvancedPanelContent.UpdateResRefDisplay(newName);
            UpdateTitle();
            UpdateRecentFilesMenu();

            UpdateStatus($"Renamed to: {newName}{extension}");
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to rename file: {ex.Message}");
            DialogHelper.ShowMessageDialog(this, "Rename Failed",
                $"Could not rename file:\n\n{ex.Message}");
        }
    }

    #endregion
}
