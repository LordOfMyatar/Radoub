using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Manifest.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Ifo;
using Radoub.Formats.Jrl;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using Radoub.Formats.Search;
using Radoub.UI.Controls;
using Radoub.UI.Services.Search;
using Radoub.UI.Utils;
using Radoub.UI.Views;
using DialogHelper = Radoub.UI.Services.DialogHelper;
using DocumentState = Radoub.UI.Services.DocumentState;
using FileOperationsHelper = Radoub.UI.Services.FileOperationsHelper;

namespace Manifest.Views;

/// <summary>
/// MainWindow core: constructor, initialization, keyboard shortcuts, and UI helpers.
/// All other responsibilities are in partial files:
///   - MainWindow.FileOps.cs: File operations (open/save/new/recent/auto-load)
///   - MainWindow.EditOps.cs: Edit operations (add/delete) and tree view
///   - MainWindow.PropertyPanel.cs: Property panel, language selection, token preview
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private JrlFile? _currentJrl;
    private readonly DocumentState _documentState = new("Manifest");
    private object? _selectedItem;
    private Language _currentViewLanguage = Language.English;

    // Convenience accessor for document state file path
    private string? _currentFilePath
    {
        get => _documentState.CurrentFilePath;
        set => _documentState.CurrentFilePath = value;
    }

    // Bindable properties for UI state
    public bool HasFile => _currentJrl != null;
    public bool HasSelection => _selectedItem != null;
    public bool CanAddEntry => HasFile && (_selectedItem is CategoryTreeItem || _selectedItem is EntryTreeItem);

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Wire up shared document state for title bar updates
        _documentState.DirtyStateChanged += () => Title = _documentState.GetTitle();

        // Initialize theme menu state (#1533)
        UpdateUseRadoubThemeMenuState();

        // Restore window position
        RestoreWindowPosition();

        // Handle window closing
        Closing += OnWindowClosing;

        // Handle command line file on startup
        Opened += OnWindowOpened;

        // Re-render token preview when expander opens (#1511)
        TokenPreviewExpander.PropertyChanged += (_, e) =>
        {
            if (e.Property == Expander.IsExpandedProperty)
                OnTokenPreviewExpanded(null, null!);
        };

        // Subscribe to theme changes to refresh module indicator color (#1859)
        Radoub.UI.Services.ThemeManager.Instance.ThemeApplied += OnThemeApplied;

        // Initialize search bar with JRL search provider
        var searchBar = this.FindControl<SearchBar>("FileSearchBar");
        searchBar?.Initialize(
            new FileSearchService(new JrlSearchProvider()),
            new (string, SearchFieldCategory)[]
            {
                ("Text", SearchFieldCategory.Content),
                ("Tags", SearchFieldCategory.Identity),
                ("Metadata", SearchFieldCategory.Metadata),
            });
        if (searchBar != null)
        {
            searchBar.FileModified += OnSearchFileModified;
            searchBar.NavigateToMatch += OnSearchNavigateToMatch;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, "Manifest MainWindow initialized");
    }

    #region Lifecycle

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        // Only handle once
        Opened -= OnWindowOpened;

        UpdateRecentFilesMenu();
        UpdateModuleIndicator();
        await HandleStartupFileAsync();
    }

    private void RestoreWindowPosition()
    {
        var settings = SettingsService.Instance;
        Radoub.UI.Services.WindowPositionHelper.Restore(this, settings);

        // Restore tree panel width
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            MainGrid.ColumnDefinitions[0].Width = new Avalonia.Controls.GridLength(settings.TreePanelWidth);
        }
    }

    private void SaveWindowPosition()
    {
        var settings = SettingsService.Instance;
        Radoub.UI.Services.WindowPositionHelper.Save(this, settings);

        // Save tree panel width
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            settings.TreePanelWidth = MainGrid.ColumnDefinitions[0].Width.Value;
        }
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        var shouldClose = await FileOperationsHelper.HandleClosingAsync(
            this, e, _documentState.IsDirty, async () => { await SaveFile(); return true; });

        if (shouldClose)
        {
            _documentState.ClearDirty();
            Radoub.UI.Services.ThemeManager.Instance.ThemeApplied -= OnThemeApplied;
            Radoub.UI.Services.FileSessionLockService.ReleaseAllLocks();
            SaveWindowPosition();
            if (e.Cancel)
            {
                // HandleClosingAsync set Cancel=true, we need to re-close
                Close();
            }
        }
    }

    #endregion

    #region Module Indicator

    /// <summary>
    /// Refresh module indicator when theme changes so BrushManager picks up
    /// the new theme's info/warning colors (#1859).
    /// </summary>
    private void OnThemeApplied(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateModuleIndicator);
    }

    /// <summary>
    /// Update status bar module indicator from RadoubSettings (#1003).
    /// Shows module name or "No module selected" in warning colors.
    /// </summary>
    private void UpdateModuleIndicator()
    {
        try
        {
            var modulePath = RadoubSettings.Instance.CurrentModulePath;

            if (!RadoubSettings.IsValidModulePath(modulePath))
            {
                StatusBar.ModuleIndicator = "No module selected";
                return;
            }

            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                modulePath = FindWorkingDirectory(modulePath);

            if (string.IsNullOrEmpty(modulePath) || !Directory.Exists(modulePath))
            {
                StatusBar.ModuleIndicator = "No module selected";
                return;
            }

            var ifoPath = Path.Combine(modulePath, "module.ifo");
            string? moduleName = null;
            if (File.Exists(ifoPath))
            {
                var ifo = IfoReader.Read(ifoPath);
                moduleName = ifo.ModuleName.GetDefault();
            }

            StatusBar.ModuleIndicator = $"Module: {moduleName ?? Path.GetFileName(modulePath)}";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogUI(LogLevel.WARN, $"Failed to update module indicator: {ex.Message}");
            StatusBar.ModuleIndicator = "No module selected";
        }
    }

    /// <summary>
    /// Find the unpacked working directory for a .mod file.
    /// Checks for module name folder, temp0, or temp1.
    /// </summary>
    private static string? FindWorkingDirectory(string modFilePath)
    {
        var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
        var moduleDir = Path.GetDirectoryName(modFilePath);

        if (string.IsNullOrEmpty(moduleDir))
            return null;

        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp1")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Navigate to a specific quest category and optionally select an entry.
    /// </summary>
    private void NavigateToQuest(string questTag, uint? entryId)
    {
        if (_currentJrl == null) return;

        // Find the category with matching tag
        var category = _currentJrl.Categories.FirstOrDefault(c =>
            string.Equals(c.Tag, questTag, StringComparison.OrdinalIgnoreCase));

        if (category == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Quest tag not found: {questTag}");
            UpdateStatus($"Quest not found: {questTag}");
            return;
        }

        // Find and select the tree item
        foreach (var treeItem in JournalTree.Items.OfType<TreeViewItem>())
        {
            if (treeItem.Tag is CategoryTreeItem catItem && catItem.Category == category)
            {
                treeItem.IsExpanded = true;

                // If entry ID specified, find and select that entry
                if (entryId.HasValue)
                {
                    var entry = category.Entries.FirstOrDefault(e => e.ID == entryId.Value);
                    if (entry != null)
                    {
                        foreach (var entTreeItem in treeItem.Items.OfType<TreeViewItem>())
                        {
                            if (entTreeItem.Tag is EntryTreeItem entItem && entItem.Entry == entry)
                            {
                                JournalTree.SelectedItem = entTreeItem;
                                entTreeItem.Focus();
                                UnifiedLogger.LogApplication(LogLevel.INFO, $"Navigated to quest '{questTag}' entry {entryId}");
                                return;
                            }
                        }
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Entry ID {entryId} not found in quest '{questTag}'");
                    }
                }

                // Select the category itself
                JournalTree.SelectedItem = treeItem;
                treeItem.Focus();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Navigated to quest '{questTag}'");
                return;
            }
        }
    }

    #endregion

    #region Keyboard Shortcuts

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Handle keyboard shortcuts
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    _ = CreateNewJournal();
                    e.Handled = true;
                    break;
                case Key.O:
                    _ = OpenFile();
                    e.Handled = true;
                    break;
                case Key.S:
                    if (HasFile)
                    {
                        _ = SaveFile();
                        e.Handled = true;
                    }
                    break;
                case Key.E:
                    if (CanAddEntry)
                    {
                        OnAddEntryClick(sender, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
                case Key.T:
                    // Insert token (Ctrl+T) - only when entry is selected
                    if (_selectedItem is EntryTreeItem)
                    {
                        OnInsertTokenClick(sender, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
                case Key.F:
                    if (HasFile)
                    {
                        OnFindClick(sender, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
                case Key.H:
                    if (HasFile)
                    {
                        OnFindReplaceClick(sender, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
            }
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            switch (e.Key)
            {
                case Key.N:
                    if (HasFile)
                    {
                        OnAddCategoryClick(sender, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
                case Key.S:
                    // Save As - future feature
                    break;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.Delete:
                    if (HasSelection)
                    {
                        OnDeleteClick(sender, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
                case Key.F1:
                    OnAboutClick(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.F3:
                    this.FindControl<SearchBar>("FileSearchBar")?.FindNext();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.Shift)
        {
            switch (e.Key)
            {
                case Key.F3:
                    this.FindControl<SearchBar>("FileSearchBar")?.FindPrevious();
                    e.Handled = true;
                    break;
            }
        }
    }

    #endregion

    #region Search

    private void OnFindClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<SearchBar>("FileSearchBar")?.Show(_currentFilePath);
    }

    private void OnFindReplaceClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<SearchBar>("FileSearchBar")?.ShowReplace(_currentFilePath);
    }

    private async void OnSearchFileModified(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            await LoadFile(_currentFilePath);
            UpdateStatus("File reloaded after replace.");
        }
    }

    private void OnSearchNavigateToMatch(object? sender, Radoub.Formats.Search.SearchMatch? match)
    {
        if (match == null) { UpdateStatus("No matches"); return; }
        var preview = match.FullFieldValue.Length > 60
            ? match.FullFieldValue[..60] + "..."
            : match.FullFieldValue;
        var locationText = match.Location switch
        {
            Radoub.Formats.Search.JrlMatchLocation jrl => jrl.DisplayPath,
            _ => ""
        };
        UpdateStatus($"Found \"{match.MatchedText}\" in {match.Field.Name}{(locationText.Length > 0 ? $" — {locationText}" : "")}: {preview}");
    }

    #endregion

    #region UI Helpers

    private void UpdateTitle()
    {
        Title = _documentState.GetTitle();
    }

    private void UpdateStatus(string message)
    {
        StatusBar.PrimaryText = message;
    }

    private void MarkDirty()
    {
        _documentState.MarkDirty();
    }

    private void ShowErrorDialog(string title, string message)
        => DialogHelper.ShowError(this, title, message);

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show(this);
    }

    private void OnEditSettingsFileClick(object? sender, RoutedEventArgs e)
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "RadoubSettings.json");

        if (!File.Exists(settingsPath))
        {
            UpdateStatus("Settings file not found");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(settingsPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to open settings file: {ex.Message}");
            UpdateStatus("Could not open settings file");
        }
    }

    private void OnToggleUseRadoubThemeClick(object? sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance;
        settings.UseSharedTheme = !settings.UseSharedTheme;
        UpdateUseRadoubThemeMenuState();
        Radoub.UI.Services.ThemeManager.Instance.ApplyEffectiveTheme(settings.CurrentThemeId, settings.UseSharedTheme);
    }

    private void UpdateUseRadoubThemeMenuState()
    {
        var menuItem = this.FindControl<MenuItem>("UseRadoubThemeMenuItem");
        if (menuItem != null)
            menuItem.Icon = SettingsService.Instance.UseSharedTheme ? new TextBlock { Text = "✓" } : null;
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = AboutWindow.Create(new AboutWindowConfig
        {
            ToolName = "Manifest",
            Subtitle = "Journal Editor for Neverwinter Nights",
            Version = VersionHelper.GetVersion(),
            IconBitmap = new Avalonia.Media.Imaging.Bitmap(
                Avalonia.Platform.AssetLoader.Open(
                    new System.Uri("avares://Manifest/Assets/manifest.ico")))
        });
        aboutWindow.Show(this);
    }

    private async void OnExportLogsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "Manifest", "Logs");

            if (!Directory.Exists(logFolder))
            {
                UpdateStatus("No logs to export");
                return;
            }

            var storageProvider = StorageProvider;
            var options = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export Logs for Support",
                SuggestedFileName = $"Manifest_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("ZIP Archive")
                    {
                        Patterns = new[] { "*.zip" }
                    }
                }
            };

            var file = await storageProvider.SaveFilePickerAsync(options);
            if (file == null) return;

            var result = file.Path.LocalPath;
            if (File.Exists(result)) File.Delete(result);

            ZipFile.CreateFromDirectory(logFolder, result);

            UpdateStatus($"Logs exported to: {Path.GetFileName(result)}");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Exported logs to: ~/{Path.GetFileName(result)}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to export logs: {ex.Message}");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to export logs: {ex.Message}");
        }
    }

    private void OnOpenLogFolderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "Manifest", "Logs");

            if (!Directory.Exists(logFolder))
            {
                UpdateStatus("Log folder does not exist yet");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = logFolder,
                UseShellExecute = true
            });

            UnifiedLogger.LogApplication(LogLevel.INFO, "Opened log folder");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to open log folder: {ex.Message}");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open log folder: {ex.Message}");
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Title Bar Handlers

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion
}

#region Tree Item Models

internal class CategoryTreeItem
{
    public JournalCategory Category { get; }

    public string DisplayName
    {
        get
        {
            var name = TlkService.Instance.ResolveLocString(Category.Name);
            if (string.IsNullOrEmpty(name))
                name = "(no name)";
            return $"[{Category.Tag}] {name}";
        }
    }

    public CategoryTreeItem(JournalCategory category)
    {
        Category = category;
    }
}

internal class EntryTreeItem
{
    public JournalEntry Entry { get; }
    public JournalCategory ParentCategory { get; }

    public string DisplayName
    {
        get
        {
            var text = TlkService.Instance.ResolveLocString(Entry.Text);
            var truncated = TruncateText(text, 40);
            return $"[{Entry.ID}] {(Entry.End ? "(END) " : "")}{truncated}";
        }
    }

    public EntryTreeItem(JournalEntry entry, JournalCategory parent)
    {
        Entry = entry;
        ParentCategory = parent;
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "(empty)";
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength - 3) + "...";
    }
}

#endregion
