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
    /// The parent window handles confirmation and actual deletion.
    /// </summary>
    public event EventHandler<FileDeleteRequestedEventArgs>? FileDeleteRequested;

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
                UpdateSearchWatermark();
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

        _filteredEntries = BrowserSortLogic.FilterAndSort(customFiltered, SearchBox?.Text, _sortMode);

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

        SortMode = requested.Value;
    }

    private void OnCollapseClick(object? sender, RoutedEventArgs e)
    {
        IsCollapsed = !IsCollapsed;
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Only enable Delete for module files (not HAK/vault resources)
        var hasSelection = FileGrid.SelectedItem is FileBrowserEntry entry
            && !entry.IsFromHak
            && !string.IsNullOrEmpty(entry.FilePath);
        DeleteMenuItem.IsEnabled = hasSelection;
    }

    private void OnDeleteMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (FileGrid.SelectedItem is FileBrowserEntry entry && !entry.IsFromHak && !string.IsNullOrEmpty(entry.FilePath))
        {
            FileDeleteRequested?.Invoke(this, new FileDeleteRequestedEventArgs(entry));
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
    /// Cancel any in-flight indexing task. Called on module change and disposal.
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
