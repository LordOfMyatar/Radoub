using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Radoub.UI.Controls;

/// <summary>
/// FileBrowserPanelBase partial: file discovery extension points and the core load/filter/list
/// pipeline. Split from the monolithic code-behind (#2426); no behavior change.
/// </summary>
public partial class FileBrowserPanelBase
{
    #region File Loading (Override in derived classes)

    /// <summary>
    /// Load files from the module path. Override to customize scanning behavior.
    /// Base implementation scans for files matching FileExtension.
    /// </summary>
    protected virtual async Task<List<FileBrowserEntry>> LoadFilesFromModuleAsync(string modulePath)
    {
        return await Task.Run(() =>
        {
            var entries = new List<FileBrowserEntry>();

            try
            {
                if (!System.IO.Directory.Exists(modulePath))
                    return entries;

                var pattern = FileExtension.StartsWith(".") ? $"*{FileExtension}" : $"*.{FileExtension}";
                var files = System.IO.Directory.GetFiles(modulePath, pattern, System.IO.SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    entries.Add(new FileBrowserEntry
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        Source = "Module",
                        IsFromHak = false
                    });
                }

                entries = entries.OrderBy(e => e.Name).ToList();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"FileBrowserPanel: Error loading files: {ex.Message}");
            }

            return entries;
        });
    }

    /// <summary>
    /// Load additional files from HAKs or other sources. Override to add HAK support.
    /// Called after LoadFilesFromModuleAsync.
    /// </summary>
    protected virtual Task<List<FileBrowserEntry>> LoadAdditionalFilesAsync()
    {
        return Task.FromResult(new List<FileBrowserEntry>());
    }

    /// <summary>
    /// Apply tool-specific filtering. Override to add custom filter logic.
    /// Base implementation only applies search text filter.
    /// </summary>
    protected virtual IEnumerable<FileBrowserEntry> ApplyCustomFilters(IEnumerable<FileBrowserEntry> entries)
    {
        return entries;
    }

    /// <summary>
    /// Format the count label text. Override to customize count display.
    /// </summary>
    protected virtual string FormatCountLabel(int moduleCount, int hakCount, int totalCount)
    {
        if (totalCount == 0)
            return "No files found";

        if (hakCount > 0)
            return $"{moduleCount} module + {hakCount} HAK";

        return $"{totalCount} file{(totalCount == 1 ? "" : "s")}";
    }

    #endregion

    #region Core Loading Logic

    private async Task LoadFilesAsync()
    {
        if (string.IsNullOrEmpty(_modulePath))
        {
            _allEntries.Clear();
            _filteredEntries.Clear();
            UpdateList();
            return;
        }

        ShowLoading("Loading files...");

        try
        {
            // Load module files
            _allEntries = await LoadFilesFromModuleAsync(_modulePath);

            // Load additional files (HAKs, vaults, etc.)
            var additionalEntries = await LoadAdditionalFilesAsync();
            foreach (var entry in additionalEntries)
            {
                if (!_allEntries.Any(e => e.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _allEntries.Add(entry);
                }
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"FileBrowserPanel: Loaded {_allEntries.Count} files from {UnifiedLogger.SanitizePath(_modulePath)}");

            ApplyFilter();
            KickoffIndexing();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"FileBrowserPanel: Error loading files: {ex.Message}");
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private void ApplyFilter()
    {
        // Apply tool-specific filters (checkbox-based) BEFORE the shared sort/search
        // so custom filters can use any FileBrowserEntry field they need.
        var customFiltered = ApplyCustomFilters(_allEntries);

        _filteredEntries = BrowserSortLogic.FilterAndSort(
            customFiltered, SearchBox?.Text, _sortMode, _sortDirection);

        UpdateList();
    }

    private void UpdateList()
    {
        // Re-assign ItemsSource so the DataGrid rebinds. Setting to a new
        // List<> instance forces a full refresh (we don't use ObservableCollection
        // because the list is fully rebuilt on every filter/sort change anyway).
        FileGrid.ItemsSource = null;
        FileGrid.ItemsSource = _filteredEntries;

        // Update count
        var moduleCount = _filteredEntries.Count(e => !e.IsFromHak);
        var hakCount = _filteredEntries.Count(e => e.IsFromHak);
        var totalCount = _filteredEntries.Count;

        CountLabel.Text = FormatCountLabel(moduleCount, hakCount, totalCount);
        CountLabel.Foreground = totalCount == 0
            ? BrushManager.GetWarningBrush(this)
            : BrushManager.GetInfoBrush(this);

        // Restore current file highlight
        HighlightCurrentFile();
    }

    private void HighlightCurrentFile()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            FileGrid.SelectedItem = null;
            return;
        }

        // Prefer exact FilePath match (disambiguates kingsnake.utc vs kingsnake.bic),
        // fall back to name match for entries without file paths (e.g., HAK resources).
        var entry = _filteredEntries.FirstOrDefault(e =>
            e.FilePath != null && e.FilePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            var currentName = System.IO.Path.GetFileNameWithoutExtension(_currentFilePath);
            entry = _filteredEntries.FirstOrDefault(e =>
                e.Name.Equals(currentName, StringComparison.OrdinalIgnoreCase));
        }

        if (entry != null)
        {
            FileGrid.SelectedItem = entry;
            FileGrid.ScrollIntoView(entry, null);
        }
    }

    #endregion
}
