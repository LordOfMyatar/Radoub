using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using Radoub.UI.Views;

namespace Radoub.UI.Controls;

/// <summary>
/// Base control for file browser panels that can be embedded in tool main windows.
/// Provides common UI (search, list, count) with extension points for tool-specific behavior.
/// </summary>
public partial class FileBrowserPanelBase : UserControl, IFileBrowserPanel
{
    private List<FileBrowserEntry> _allEntries = new();
    private List<FileBrowserEntry> _filteredEntries = new();
    private string? _modulePath;
    private string? _currentFilePath;
    private bool _isCollapsed;
    private BrowserSortMode _sortMode = BrowserSortMode.ResRef;
    private BrowserSortDirection _sortDirection = BrowserSortDirection.Ascending;
    private CancellationTokenSource? _indexingCts;

    // DataGrid column indices (must match AXAML column order)
    private const int ResRefColumnIndex = 0;
    private const int NameColumnIndex = 1;
    private const int TagColumnIndex = 2;

    private DataGridColumn ResRefColumn => FileGrid.Columns[ResRefColumnIndex];
    private DataGridColumn NameColumn => FileGrid.Columns[NameColumnIndex];
    private DataGridColumn TagColumn => FileGrid.Columns[TagColumnIndex];

    /// <summary>
    /// Raised when a file is selected (single-click) or activated (double-click).
    /// </summary>
    public event EventHandler<FileSelectedEventArgs>? FileSelected;

    /// <summary>
    /// Raised when the user requests deletion of a file from the browser panel.
    /// </summary>
    /// <remarks>
    /// DEPRECATED (#2350): the base now owns the confirm + backup + delete + refresh
    /// itself so every tool inherits delete-with-backup. Hosts should subscribe to
    /// <see cref="FileDeleted"/> for editor cleanup instead of handling this event.
    /// Retained only for back-compat; no longer raised by the base.
    /// </remarks>
    public event EventHandler<FileDeleteRequestedEventArgs>? FileDeleteRequested;

    /// <summary>
    /// Raised AFTER the panel backs up, deletes a module file on disk, and refreshes
    /// the browser (#2350). The disk delete and browser refresh are already done — the
    /// host must not repeat them. The host reacts to fix editor state (e.g. close the
    /// file if the deleted one was open) and update the status bar.
    /// <see cref="FileDeletedEventArgs.WasCurrentFile"/> is true when the deleted file
    /// was the one set via <see cref="CurrentFilePath"/>.
    /// </summary>
    public event EventHandler<FileDeletedEventArgs>? FileDeleted;

    /// <summary>
    /// Raised when the panel's collapsed state changes.
    /// </summary>
    public event EventHandler<bool>? CollapsedChanged;

    /// <summary>
    /// Raised when an archive resource is successfully copied to the module folder.
    /// The string is the destination file path. Consumers typically update a status bar.
    /// </summary>
    public event EventHandler<string>? FileCopiedToModule;

    /// <summary>
    /// Raised when the user asks to rename the file CURRENTLY OPEN in the editor
    /// (#2320). The panel does not touch the file; the host runs its own
    /// lock-aware save-rename-reload (the open file may hold a session lock and
    /// has an in-memory ResRef the panel can't see), then refreshes the browser.
    /// </summary>
    public event EventHandler<FileRenameRequestedEventArgs>? FileRenameRequested;

    /// <summary>
    /// Raised AFTER the panel renames a NON-open module file on disk and
    /// refreshes the browser (#2320). The host updates the status bar. The disk
    /// move and the browser refresh are already done — the host must not repeat
    /// them. (Renaming the open file goes through <see cref="FileRenameRequested"/>
    /// instead.)
    /// </summary>
    public event EventHandler<FileRenamedEventArgs>? FileRenamed;

    /// <summary>
    /// Raised AFTER the panel copies a module file on disk and refreshes the
    /// browser (#2320). The host typically updates the status bar. The disk copy
    /// and the browser refresh are already done.
    /// </summary>
    public event EventHandler<FileCopiedEventArgs>? FileCopied;

    /// <summary>
    /// The header text displayed at the top of the panel.
    /// </summary>
    public static readonly StyledProperty<string> HeaderTextProperty =
        AvaloniaProperty.Register<FileBrowserPanelBase, string>(nameof(HeaderTextContent), "Files");

    public string HeaderTextContent
    {
        get => GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    /// <summary>
    /// Optional content for tool-specific filter options (e.g., "Show HAK" checkbox).
    /// </summary>
    public static readonly StyledProperty<object?> FilterOptionsContentProperty =
        AvaloniaProperty.Register<FileBrowserPanelBase, object?>(nameof(FilterOptionsContent));

    public object? FilterOptionsContent
    {
        get => GetValue(FilterOptionsContentProperty);
        set => SetValue(FilterOptionsContentProperty, value);
    }

    /// <summary>
    /// File extension to scan for (e.g., ".dlg", ".utm"). Set by derived classes.
    /// </summary>
    protected string FileExtension { get; set; } = ".*";

    /// <summary>
    /// Watermark text for the search box.
    /// </summary>
    protected string SearchWatermark
    {
        get => SearchBox.Watermark ?? "";
        set => SearchBox.Watermark = value;
    }

    /// <summary>
    /// Sort modes this panel exposes in the SortMode ComboBox. Default is all
    /// three modes. Override to restrict — e.g., DialogBrowserPanel returns
    /// [ResRef] because DLG has no Tag/Name fields.
    /// </summary>
    protected virtual IReadOnlyList<BrowserSortMode> SupportedSortModes { get; } = new[]
    {
        BrowserSortMode.ResRef,
        BrowserSortMode.Name,
        BrowserSortMode.Tag
    };

    /// <summary>
    /// Current sort/search mode. Setting this re-applies the filter and updates
    /// the search-box watermark. Usually changed indirectly by clicking a
    /// DataGrid column header.
    /// </summary>
    public BrowserSortMode SortMode
    {
        get => _sortMode;
        set
        {
            if (_sortMode != value)
            {
                _sortMode = value;
                // Switching column resets to ascending (matches column-header UX).
                _sortDirection = BrowserSortDirection.Ascending;
                UpdateSearchWatermark();
                SyncColumnSortIndicators();
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// Current sort direction for the active <see cref="SortMode"/>. Toggled by
    /// repeat clicks on the same DataGrid column header; resets to Ascending
    /// when switching to a different column (#2200).
    /// </summary>
    public BrowserSortDirection SortDirection
    {
        get => _sortDirection;
        set
        {
            if (_sortDirection != value)
            {
                _sortDirection = value;
                SyncColumnSortIndicators();
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// Populate <see cref="FileBrowserEntry.DisplayLabel"/> and
    /// <see cref="FileBrowserEntry.Tag"/> for the given entries in the background.
    /// Default no-op. Tools override (Relique/Fence/Quartermaster) to read GFF
    /// fields and/or pull from <see cref="SharedPaletteCacheService"/>.
    /// </summary>
    /// <param name="entries">Entries that still need metadata
    /// (<see cref="FileBrowserEntry.MetadataLoaded"/> is false). The base loop
    /// passes only un-indexed entries; the override may skip any it can't handle.</param>
    /// <param name="cancellationToken">Cancelled on module change or when this
    /// panel is disposed. Honor it between batches.</param>
    protected virtual Task IndexMetadataAsync(
        IReadOnlyList<FileBrowserEntry> entries,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Re-read metadata for a single entry — typically called by the host tool
    /// after a save so the browser row reflects the new Tag/Name without a full
    /// reindex. Default no-op.
    /// </summary>
    public virtual Task RefreshEntryMetadataAsync(FileBrowserEntry entry)
        => Task.CompletedTask;

    public FileBrowserPanelBase()
    {
        InitializeComponent();
        HeaderText.Bind(TextBlock.TextProperty, this.GetObservable(HeaderTextProperty));

        // Update Delete menu item state when context menu opens
        FileListContextMenu.Opening += OnContextMenuOpening;

        // Lazily add Copy-to-Module menu item once the visual tree is ready so
        // subclass constructors have finished setting FileExtension/SupportsCopyToModule.
        Loaded += (_, _) =>
        {
            TryAddCopyToModuleMenuItem();
            InitializeSortColumns();
        };
    }

    #region IFileBrowserPanel Implementation

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                _isCollapsed = value;
                UpdateCollapsedState();
                CollapsedChanged?.Invoke(this, value);
            }
        }
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set
        {
            _currentFilePath = value;
            HighlightCurrentFile();
        }
    }

    public string? ModulePath
    {
        get => _modulePath;
        set
        {
            if (_modulePath != value)
            {
                _modulePath = value;
                CancelIndexing();
                _ = RefreshAsync();
            }
        }
    }

    public int FileCount => _filteredEntries.Count;

    public async Task RefreshAsync()
    {
        await LoadFilesAsync();
    }

    public void SetFilter(string searchText)
    {
        SearchBox.Text = searchText;
        ApplyFilter();
    }

    #endregion

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

    #region UI Helpers

    private void UpdateCollapsedState()
    {
        CollapseButton.Content = _isCollapsed ? "▶" : "◀";
        ToolTip.SetTip(CollapseButton, _isCollapsed ? "Expand panel" : "Collapse panel");

        // The actual collapse animation should be handled by the parent container
        // This just updates the button state
    }

    protected void ShowLoading(string message)
    {
        StatusPanel.IsVisible = true;
        StatusText.Text = message;
    }

    protected void HideLoading()
    {
        StatusPanel.IsVisible = false;
    }

    protected void ShowError(string message)
    {
        CountLabel.Text = message;
        CountLabel.Foreground = BrushManager.GetErrorBrush(this);
    }

    /// <summary>
    /// Update the status text during loading operations.
    /// </summary>
    protected void UpdateLoadingStatus(string message)
    {
        StatusText.Text = message;
    }

    #endregion

    #region Event Handlers

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnFileGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FileGrid.SelectedItem is FileBrowserEntry entry)
        {
            FileSelected?.Invoke(this, new FileSelectedEventArgs(entry, isDoubleClick: false));
        }
    }

    private void OnFileGridDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is FileBrowserEntry entry)
        {
            FileSelected?.Invoke(this, new FileSelectedEventArgs(entry, isDoubleClick: true));
        }
    }

    /// <summary>
    /// Right-click should select the row under the pointer before the context menu
    /// opens (#2106 — fixes Copy-to-Module visibility on right-click). We walk up
    /// the visual tree from the click source to the DataGridRow and select its
    /// DataContext so context-menu Opening handlers see a non-null SelectedItem.
    /// </summary>
    private void OnFileGridContextRequested(object? sender, Avalonia.Controls.ContextRequestedEventArgs e)
    {
        var source = e.Source as Avalonia.Visual;
        while (source != null && source is not DataGridRow)
        {
            source = source.GetVisualParent();
        }

        if (source is DataGridRow row && row.DataContext is FileBrowserEntry entry)
        {
            FileGrid.SelectedItem = entry;
        }
    }

    /// <summary>
    /// Intercept DataGrid column-header sorts: translate the clicked column into
    /// a <see cref="BrowserSortMode"/>, set <see cref="SortMode"/> (which re-applies
    /// the filter), and cancel the built-in sort so module-first tier is preserved.
    /// </summary>
    private void OnFileGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        BrowserSortMode? requested = null;
        if (ReferenceEquals(e.Column, ResRefColumn)) requested = BrowserSortMode.ResRef;
        else if (ReferenceEquals(e.Column, NameColumn)) requested = BrowserSortMode.Name;
        else if (ReferenceEquals(e.Column, TagColumn)) requested = BrowserSortMode.Tag;

        if (requested == null) return;

        // Suppress DataGrid's built-in sort (it would override our module-first tier).
        e.Handled = true;

        // Repeat-click on the active column flips direction; switching column
        // resets to ascending (the SortMode setter handles the reset).
        if (requested.Value == _sortMode)
        {
            SortDirection = _sortDirection == BrowserSortDirection.Ascending
                ? BrowserSortDirection.Descending
                : BrowserSortDirection.Ascending;
        }
        else
        {
            SortMode = requested.Value;
        }
    }

    /// <summary>
    /// No-op on Avalonia: the Avalonia DataGrid doesn't expose a settable
    /// SortDirection on DataGridColumn (WPF-only API). The built-in arrow
    /// indicator follows whichever column was last clicked, which is good
    /// enough — the actual sort state is reflected in the visible row order.
    /// Kept as a named seam so future Avalonia versions or a custom header
    /// adornment can plug in without changing the call sites.
    /// </summary>
    private void SyncColumnSortIndicators()
    {
        // Intentionally empty. See remarks above.
    }

    private void OnCollapseClick(object? sender, RoutedEventArgs e)
    {
        IsCollapsed = !IsCollapsed;
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Copy/Rename/Delete operate on on-disk module files only (not HAK/BIF
        // archive resources, which have no FilePath).
        var isModuleFile = FileGrid.SelectedItem is FileBrowserEntry entry
            && !entry.IsFromHak
            && !string.IsNullOrEmpty(entry.FilePath);
        DeleteMenuItem.IsEnabled = isModuleFile;
        CopyMenuItem.IsEnabled = isModuleFile;
        RenameMenuItem.IsEnabled = isModuleFile;
    }

    /// <summary>
    /// Confirm, back up, then delete a module file on disk (#2350). The confirm
    /// dialog, backup (to ~/Radoub/Backups/{module}/{timestamp}/), delete, and
    /// browser refresh are all handled here so every tool gets identical
    /// data-safe behavior — no per-tool hand-rolled File.Delete that loses data
    /// on a misclick. The host only reacts to <see cref="FileDeleted"/> to close
    /// the open file and update the status bar.
    /// </summary>
    private async void OnDeleteMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is not FileBrowserEntry entry
            || entry.IsFromHak || string.IsNullOrEmpty(entry.FilePath))
            return;

        var filePath = entry.FilePath!;
        if (!File.Exists(filePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Delete source missing: {UnifiedLogger.SanitizePath(filePath)}");
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var fileName = Path.GetFileName(filePath);
        var confirmed = await DialogHelper.ShowConfirmAsync(
            owner, "Confirm Delete",
            $"Delete \"{fileName}\" from disk?\n\nA backup is saved to ~/Radoub/Backups first, so this can be restored.");
        if (!confirmed) return;

        var wasCurrentFile = !string.IsNullOrEmpty(_currentFilePath)
            && filePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase);

        try
        {
            // Release the file session lock before deleting the open file — the lock
            // sidecar lives next to the file and would otherwise survive the delete,
            // blocking other tools from editing a recreated file (#2257). No-op if
            // unlocked.
            if (wasCurrentFile)
                FileSessionLockService.ReleaseLock(filePath);

            // Back up before deleting so a misclick is recoverable (#2347/#2350).
            var modulePath = Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath;
            var moduleName = !string.IsNullOrEmpty(modulePath)
                ? Path.GetFileNameWithoutExtension(modulePath)
                : "unknown";
            await Services.Search.FileDeletionService.DeleteWithBackupAsync(
                filePath, moduleName, new Services.Search.BackupService());
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Deleted file (backed up): {UnifiedLogger.SanitizePath(filePath)}");

            await RefreshAsync();
            FileDeleted?.Invoke(this, new FileDeletedEventArgs(filePath, wasCurrentFile));
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Delete failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Delete failed (access): {ex.Message}");
        }
    }

    /// <summary>
    /// Copy a module file to a new ResRef in the same directory (#2320). The
    /// disk copy, browser refresh, and post-event are all handled here so every
    /// tool gets identical behavior — no per-tool refresh drift. The host only
    /// reacts to <see cref="FileCopied"/> for status-bar feedback.
    /// </summary>
    private async void OnCopyMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is not FileBrowserEntry entry
            || entry.IsFromHak || string.IsNullOrEmpty(entry.FilePath))
            return;

        var sourcePath = entry.FilePath!;
        if (!File.Exists(sourcePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Copy source missing: {UnifiedLogger.SanitizePath(sourcePath)}");
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var currentName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);

        var newName = await RenameDialog.ShowAsync(
            owner, currentName, directory, extension, actionLabel: "Copy", allowUnchanged: true);
        if (string.IsNullOrEmpty(newName)) return; // cancelled

        var resolved = FileBrowserOperations.ResolveCopyDestination(sourcePath, newName, extension);
        if (!resolved.IsValid || resolved.DestinationPath == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Copy rejected: {resolved.ErrorMessage}");
            return;
        }

        try
        {
            // overwrite:false — the dialog already rejected an existing target.
            File.Copy(sourcePath, resolved.DestinationPath, overwrite: false);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Copied file: {UnifiedLogger.SanitizePath(sourcePath)} -> {UnifiedLogger.SanitizePath(resolved.DestinationPath)}");

            await RefreshAsync();
            FileCopied?.Invoke(this, new FileCopiedEventArgs(sourcePath, resolved.DestinationPath));
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Copy failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Copy failed (access): {ex.Message}");
        }
    }

    /// <summary>
    /// Rename a module file on disk to a new ResRef in the same directory
    /// (#2320). The disk move, browser refresh (drop stale row + re-scan), and
    /// post-event are handled here. The host reacts to <see cref="FileRenamed"/>
    /// to fix editor state — e.g. update the open-file path if the renamed file
    /// was the one loaded — and the status bar.
    /// </summary>
    private async void OnRenameMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is not FileBrowserEntry entry
            || entry.IsFromHak || string.IsNullOrEmpty(entry.FilePath))
            return;

        var sourcePath = entry.FilePath!;
        if (!File.Exists(sourcePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Rename source missing: {UnifiedLogger.SanitizePath(sourcePath)}");
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var currentName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);

        // Prompt + validate up front so both paths (open / not-open) share the
        // exact same name validation. Clicking a row to reach this menu usually
        // also loads it, so the open-file case is the common one (#2320).
        var newName = await RenameDialog.ShowAsync(
            owner, currentName, directory, extension, actionLabel: "Rename", allowUnchanged: false);
        if (string.IsNullOrEmpty(newName)) return; // cancelled

        var resolved = FileBrowserOperations.ResolveRenameDestination(sourcePath, newName, extension);
        if (!resolved.IsValid || resolved.DestinationPath == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"FileBrowserPanel: Rename rejected: {resolved.ErrorMessage}");
            return;
        }

        // Renaming the currently-open file is the host's job — it holds the
        // session lock and the in-memory ResRef the panel can't see. Hand it the
        // already-validated destination and let it save → move → reload.
        if (!string.IsNullOrEmpty(_currentFilePath)
            && sourcePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase))
        {
            FileRenameRequested?.Invoke(this,
                new FileRenameRequestedEventArgs(entry, sourcePath, resolved.DestinationPath));
            return;
        }

        try
        {
            File.Move(sourcePath, resolved.DestinationPath);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Renamed file: {UnifiedLogger.SanitizePath(sourcePath)} -> {UnifiedLogger.SanitizePath(resolved.DestinationPath)}");

            // Drop the stale pre-rename row before re-scanning so it doesn't
            // linger pointing at a path that no longer exists (#2285 pattern).
            RemoveEntryByFilePath(sourcePath);
            await RefreshAsync();
            FileRenamed?.Invoke(this, new FileRenamedEventArgs(sourcePath, resolved.DestinationPath));
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Rename failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"FileBrowserPanel: Rename failed (access): {ex.Message}");
        }
    }

    /// <summary>
    /// Call this from derived classes when custom filters change (e.g., "Show HAK" checkbox).
    /// </summary>
    protected void OnFilterOptionsChanged()
    {
        ApplyFilter();
    }

    /// <summary>
    /// Merge additional entries into the master list (with name-based dedup).
    /// Call after lazy-loading entries (e.g., on checkbox toggle) so they
    /// become visible to ApplyFilter/ApplyCustomFilters. Also triggers a
    /// background indexing pass over any newly added entries.
    /// </summary>
    protected void MergeAdditionalEntries(IEnumerable<FileBrowserEntry> entries)
    {
        var materialized = entries.ToList();
        MergeEntries(_allEntries, materialized);
        KickoffIndexing();
    }

    /// <summary>
    /// Merge source entries into target list, skipping entries whose Name
    /// already exists (case-insensitive). Extracted for testability.
    /// </summary>
    internal static void MergeEntries(List<FileBrowserEntry> target, IEnumerable<FileBrowserEntry> source)
    {
        foreach (var entry in source)
        {
            if (!target.Any(e => e.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
            {
                target.Add(entry);
            }
        }
    }

    /// <summary>
    /// Locate a browser entry by full file path (case-insensitive). Host tools
    /// call this after saving a file so they can hand the entry to
    /// <see cref="RefreshEntryMetadataAsync"/> for a targeted re-read (#2199).
    /// Returns null when the path is empty/null, no entry matches, or the
    /// matching entry has no FilePath (HAK/BIF rows).
    /// </summary>
    public FileBrowserEntry? FindEntryByFilePath(string filePath)
        => FindEntryByFilePath(_allEntries, filePath);

    /// <summary>
    /// Select the grid row whose FilePath matches <paramref name="filePath"/>, if present.
    /// Used after a new-file save reloads the list so the new row is highlighted (#2413).
    /// No-op when no matching row exists.
    /// </summary>
    public void SelectEntryByFilePath(string filePath)
    {
        var entry = FindEntryByFilePath(filePath);
        if (entry != null) FileGrid.SelectedItem = entry;
    }

    /// <summary>
    /// Pure-logic overload for testing. Same semantics as the instance method
    /// but operates on a caller-supplied entry list.
    /// </summary>
    internal static FileBrowserEntry? FindEntryByFilePath(
        IEnumerable<FileBrowserEntry> entries,
        string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.FilePath)
                && entry.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>
    /// Remove the entry whose FilePath matches <paramref name="filePath"/> and
    /// rebind the DataGrid. Host tools call this after a rename so the stale
    /// pre-rename row doesn't linger pointing at a path that no longer exists
    /// (#2285). Returns true when an entry was found and removed; false on
    /// null/empty path or unknown path.
    /// </summary>
    public bool RemoveEntryByFilePath(string filePath)
    {
        var removed = RemoveEntryByFilePath(_allEntries, filePath);
        if (removed) ApplyFilter();
        return removed;
    }

    /// <summary>
    /// Pure-logic overload for testing. Same semantics as the instance method
    /// but operates on a caller-supplied entry list. Returns true when an
    /// entry was removed.
    /// </summary>
    internal static bool RemoveEntryByFilePath(List<FileBrowserEntry> entries, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var index = entries.FindIndex(e =>
            !string.IsNullOrEmpty(e.FilePath)
            && e.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        entries.RemoveAt(index);
        return true;
    }

    #endregion

    #region Sort Mode + Indexing

    /// <summary>
    /// Apply the panel's <see cref="SupportedSortModes"/> to the DataGrid columns:
    /// hide unsupported columns (e.g., Parley DLG hides Name/Tag), and update
    /// the search-box watermark to match the current SortMode.
    /// </summary>
    private void InitializeSortColumns()
    {
        var modes = SupportedSortModes ?? new[] { BrowserSortMode.ResRef };
        var modeSet = new HashSet<BrowserSortMode>(modes);

        NameColumn.IsVisible = modeSet.Contains(BrowserSortMode.Name);
        TagColumn.IsVisible = modeSet.Contains(BrowserSortMode.Tag);

        UpdateSearchWatermark();
        SyncColumnSortIndicators();
    }

    private void UpdateSearchWatermark()
    {
        SearchBox.Watermark = _sortMode switch
        {
            BrowserSortMode.Name => "Search by name...",
            BrowserSortMode.Tag => "Search by tag...",
            _ => "Search by resref..."
        };
    }

    /// <summary>
    /// Cancel any in-flight indexing task. Called on module change and on
    /// detach (#2262) so a host disposing the panel mid-index doesn't orphan
    /// the CancellationTokenSource and its background Task.
    /// </summary>
    private void CancelIndexing()
    {
        if (_indexingCts != null)
        {
            _indexingCts.Cancel();
            _indexingCts.Dispose();
            _indexingCts = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Free the in-flight indexing CTS so detaching the panel mid-index
        // doesn't leak it until the next ModulePath setter (#2262).
        CancelIndexing();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// Start a background indexing pass for any entries that don't yet have
    /// metadata. Cancels and replaces any in-flight indexing. On completion,
    /// re-applies the current filter so searches that ran during indexing
    /// see fresh DisplayLabel/Tag data.
    /// </summary>
    private void KickoffIndexing()
    {
        CancelIndexing();

        var pending = _allEntries.Where(e => !e.MetadataLoaded).ToList();
        if (pending.Count == 0) return;

        _indexingCts = new CancellationTokenSource();
        var token = _indexingCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await IndexMetadataAsync(pending, token);

                if (token.IsCancellationRequested) return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    ApplyFilter();
                });
            }
            catch (OperationCanceledException)
            {
                // Expected on module change — no logging needed.
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"FileBrowserPanel: Indexing error: {ex.Message}");
            }
        }, token);
    }

    #endregion

    #region Copy-to-Module (shared across all derived panels)

    /// <summary>
    /// Whether this panel exposes a "Copy to Module" context menu item for
    /// archive-sourced entries. Override to return true in panels that wrap
    /// a GFF file format (UTM/UTC/UTI/DLG).
    /// </summary>
    protected virtual bool SupportsCopyToModule() => false;

    /// <summary>
    /// Whether the Copy-to-Module dialog should show editable Tag/Name fields.
    /// UTM/UTC/UTI: true. DLG (Parley): false — ResRef only.
    /// </summary>
    protected virtual bool SupportsTagNameRename() => true;

    /// <summary>
    /// Extract the raw bytes of an archive-sourced entry (BIF or HAK).
    /// Returning null aborts the copy. Default implementation returns null.
    /// </summary>
    protected virtual Task<byte[]?> ExtractArchiveBytesAsync(FileBrowserEntry entry)
        => Task.FromResult<byte[]?>(null);

    /// <summary>
    /// Read the source resource's Tag and default-language Name so the dialog
    /// can pre-fill those fields. Default returns empty strings.
    /// Only called when <see cref="SupportsTagNameRename"/> returns true.
    /// </summary>
    protected virtual Task<(string tag, string name)> ReadSourceMetadataAsync(byte[] bytes)
        => Task.FromResult((string.Empty, string.Empty));

    /// <summary>
    /// Apply the user's Tag/Name/ResRef choices to the raw source bytes, returning
    /// the bytes that should be written to the module file. Default implementation
    /// returns input bytes unchanged (Parley path — ResRef-only rename).
    /// </summary>
    protected virtual Task<byte[]> ApplyCopyCustomizationsAsync(byte[] sourceBytes, CopyToModuleResult result)
        => Task.FromResult(sourceBytes);

    /// <summary>
    /// Shared HAK extraction helper — looks up a resource in a HAK/ERF by ResRef +
    /// resource type, returns the decompressed bytes, or null on any failure.
    /// </summary>
    protected static byte[]? ExtractFromHak(string hakPath, string resRef, ushort resourceType)
    {
        try
        {
            var erf = ErfReader.ReadMetadataOnly(hakPath);
            var entry = erf.Resources.FirstOrDefault(r =>
                r.ResRef.Equals(resRef, StringComparison.OrdinalIgnoreCase)
                && r.ResourceType == resourceType);

            return entry == null ? null : ErfReader.ExtractResource(hakPath, entry);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to extract {resRef} from {Path.GetFileName(hakPath)}: {ex.Message}");
            return null;
        }
    }

    private MenuItem? _copyToModuleMenuItem;

    private void TryAddCopyToModuleMenuItem()
    {
        if (_copyToModuleMenuItem != null) return;
        if (!SupportsCopyToModule()) return;

        _copyToModuleMenuItem = new MenuItem { Header = "Copy to Module" };
        _copyToModuleMenuItem.Click += async (_, _) =>
        {
            if (FileGrid.SelectedItem is FileBrowserEntry entry)
                await CopyArchiveEntryToModuleAsync(entry);
        };

        // Insert before Delete so it appears at the top of the menu.
        FileListContextMenu.Items.Insert(0, _copyToModuleMenuItem);
        FileListContextMenu.Items.Insert(1, new Separator());

        FileListContextMenu.Opening += OnCopyToModuleContextMenuOpening;
    }

    private void OnCopyToModuleContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_copyToModuleMenuItem == null) return;

        var entry = FileGrid.SelectedItem as FileBrowserEntry;
        var isArchive = entry != null && IsArchiveEntry(entry);
        _copyToModuleMenuItem.IsVisible = isArchive && !string.IsNullOrEmpty(ModulePath);
    }

    /// <summary>
    /// Check whether an entry came from an archive (HAK or BIF) — i.e. it's
    /// a candidate for Copy-to-Module. Derived panels with their own "IsFromBif"
    /// flag override this to also accept BIF entries.
    /// </summary>
    protected virtual bool IsArchiveEntry(FileBrowserEntry entry) => entry.IsFromHak;

    private async Task CopyArchiveEntryToModuleAsync(FileBrowserEntry entry)
    {
        if (string.IsNullOrEmpty(ModulePath)) return;
        if (!SupportsCopyToModule())
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                "CopyArchiveEntryToModuleAsync called on a panel that does not support copy-to-module");
            return;
        }

        try
        {
            var bytes = await ExtractArchiveBytesAsync(entry);
            if (bytes == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Could not extract {entry.Name} from archive");
                return;
            }

            var (tag, name) = SupportsTagNameRename()
                ? await ReadSourceMetadataAsync(bytes)
                : (string.Empty, string.Empty);

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    "Cannot show CopyToModuleDialog — no owner window");
                return;
            }

            var dialogResult = await CopyToModuleDialog.ShowAsync(
                owner,
                currentResRef: entry.Name,
                currentTag: tag,
                currentName: name,
                moduleDirectory: ModulePath,
                extension: FileExtension,
                showTagAndName: SupportsTagNameRename());

            if (dialogResult == null) return; // user cancelled

            var modifiedBytes = await ApplyCopyCustomizationsAsync(bytes, dialogResult);

            var destPath = Path.Combine(ModulePath, dialogResult.NewResRef + FileExtension);
            if (File.Exists(destPath))
            {
                // Dialog already validated this, but re-check defensively.
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Destination already exists: {Path.GetFileName(destPath)}");
                return;
            }

            await File.WriteAllBytesAsync(destPath, modifiedBytes);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Copied archive resource to module: {UnifiedLogger.SanitizePath(destPath)}");

            FileCopiedToModule?.Invoke(this, destPath);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to copy {entry.Name} to module: {ex.Message}");
        }
    }

    #endregion
}
