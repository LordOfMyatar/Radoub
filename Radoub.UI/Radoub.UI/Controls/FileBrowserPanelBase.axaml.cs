using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Radoub.Formats.Logging;

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

    /// <summary>
    /// Raised when a file is selected (single-click) or activated (double-click).
    /// </summary>
    public event EventHandler<FileSelectedEventArgs>? FileSelected;

    /// <summary>
    /// Raised when the panel's collapsed state changes.
    /// </summary>
    public event EventHandler<bool>? CollapsedChanged;

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

    public FileBrowserPanelBase()
    {
        InitializeComponent();
        HeaderText.Bind(TextBlock.TextProperty, this.GetObservable(HeaderTextProperty));
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
        FileListBox.Items.Clear();

        var searchText = SearchBox?.Text?.ToLowerInvariant() ?? "";

        // Start with all entries
        IEnumerable<FileBrowserEntry> filtered = _allEntries;

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filtered = filtered.Where(e => e.Name.ToLowerInvariant().Contains(searchText));
        }

        // Apply tool-specific filters
        filtered = ApplyCustomFilters(filtered);

        // Sort: module files first, then by name
        _filteredEntries = filtered
            .OrderBy(e => e.IsFromHak ? 1 : 0)
            .ThenBy(e => e.Name)
            .ToList();

        UpdateList();
    }

    private void UpdateList()
    {
        FileListBox.Items.Clear();

        foreach (var entry in _filteredEntries)
        {
            FileListBox.Items.Add(entry);
        }

        // Update count
        var moduleCount = _filteredEntries.Count(e => !e.IsFromHak);
        var hakCount = _filteredEntries.Count(e => e.IsFromHak);
        var totalCount = _filteredEntries.Count;

        CountLabel.Text = FormatCountLabel(moduleCount, hakCount, totalCount);
        CountLabel.Foreground = totalCount == 0
            ? new SolidColorBrush(Colors.Orange)
            : new SolidColorBrush(Colors.White);

        // Restore current file highlight
        HighlightCurrentFile();
    }

    private void HighlightCurrentFile()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            FileListBox.SelectedItem = null;
            return;
        }

        var currentName = System.IO.Path.GetFileNameWithoutExtension(_currentFilePath);
        var entry = _filteredEntries.FirstOrDefault(e =>
            e.Name.Equals(currentName, StringComparison.OrdinalIgnoreCase) ||
            (e.FilePath != null && e.FilePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase)));

        if (entry != null)
        {
            FileListBox.SelectedItem = entry;
            FileListBox.ScrollIntoView(entry);
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
        CountLabel.Foreground = new SolidColorBrush(Colors.Red);
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

    private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (FileListBox.SelectedItem is FileBrowserEntry entry)
        {
            FileSelected?.Invoke(this, new FileSelectedEventArgs(entry, isDoubleClick: false));
        }
    }

    private void OnFileDoubleClicked(object? sender, RoutedEventArgs e)
    {
        if (FileListBox.SelectedItem is FileBrowserEntry entry)
        {
            FileSelected?.Invoke(this, new FileSelectedEventArgs(entry, isDoubleClick: true));
        }
    }

    private void OnCollapseClick(object? sender, RoutedEventArgs e)
    {
        IsCollapsed = !IsCollapsed;
    }

    /// <summary>
    /// Call this from derived classes when custom filters change (e.g., "Show HAK" checkbox).
    /// </summary>
    protected void OnFilterOptionsChanged()
    {
        ApplyFilter();
    }

    #endregion
}
