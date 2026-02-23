using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Helper for populating a recent files MenuItem with entries from a settings service.
/// Eliminates duplicated menu population code across tools.
///
/// Usage:
///   RecentFilesMenuHelper.Populate(
///       recentFilesMenuItem,
///       settingsService.RecentFiles,
///       filePath => LoadFile(filePath),
///       () => { settingsService.ClearRecentFiles(); UpdateRecentFilesMenu(); }
///   );
/// </summary>
public static class RecentFilesMenuHelper
{
    /// <summary>
    /// Populates a MenuItem with recent file entries.
    /// </summary>
    /// <param name="menuItem">The parent MenuItem to populate (e.g., "Open Recent")</param>
    /// <param name="recentFiles">List of file paths from the settings service</param>
    /// <param name="onFileClick">Callback when a file is clicked. Receives the file path.
    /// The callback should handle missing files (remove from settings, refresh menu).</param>
    /// <param name="onClearClick">Callback when "Clear Recent Files" is clicked.</param>
    /// <param name="sanitizePaths">If true, shows sanitized paths in tooltips (default: true)</param>
    public static void Populate(
        MenuItem menuItem,
        IReadOnlyList<string> recentFiles,
        Action<string> onFileClick,
        Action onClearClick,
        bool sanitizePaths = true)
    {
        if (menuItem == null) return;

        try
        {
            menuItem.Items.Clear();

            if (recentFiles.Count == 0)
            {
                var emptyItem = new MenuItem { Header = "(No recent files)", IsEnabled = false };
                menuItem.Items.Add(emptyItem);
                return;
            }

            foreach (var filePath in recentFiles)
            {
                var fileName = Path.GetFileName(filePath);
                // Escape underscores for Avalonia menu mnemonic handling
                var displayName = fileName.Replace("_", "__");

                var item = new MenuItem
                {
                    Header = displayName,
                    Tag = filePath
                };

                if (sanitizePaths)
                {
                    ToolTip.SetTip(item, UnifiedLogger.SanitizePath(filePath));
                }
                else
                {
                    ToolTip.SetTip(item, filePath);
                }

                item.Click += (sender, e) =>
                {
                    if (sender is MenuItem clicked && clicked.Tag is string path)
                    {
                        // Close menu hierarchy before triggering action
                        if (menuItem.Parent is MenuItem parentMenu)
                        {
                            parentMenu.Close();
                        }

                        onFileClick(path);
                    }
                };

                menuItem.Items.Add(item);
            }

            menuItem.Items.Add(new Separator());

            var clearItem = new MenuItem { Header = "Clear Recent Files" };
            clearItem.Click += (s, e) => onClearClick();
            menuItem.Items.Add(clearItem);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error building recent files menu: {ex.Message}");
        }
    }
}
