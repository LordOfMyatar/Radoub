using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;

namespace Radoub.UI.Controls;

/// <summary>
/// Base control for file browser panels that can be embedded in tool main windows.
/// Provides common UI (search, list, count) with extension points for tool-specific behavior.
///
/// The implementation is split across partial files (#2426): this file holds the shared state
/// (fields, events, properties) and the lifecycle/IFileBrowserPanel surface; the load/filter
/// pipeline, UI helpers, on-disk file operations, indexing, and Copy-to-Module live in
/// <c>FileBrowserPanelBase.Loading.cs</c>, <c>.UI.cs</c>, <c>.FileOps.cs</c>, <c>.Indexing.cs</c>,
/// and <c>.CopyToModule.cs</c> respectively.
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
}
