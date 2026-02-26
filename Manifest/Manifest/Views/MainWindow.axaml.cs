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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

        // Restore window position
        RestoreWindowPosition();

        // Handle window closing
        Closing += OnWindowClosing;

        // Handle command line file on startup
        Opened += OnWindowOpened;

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
    /// Update status bar module indicator from RadoubSettings (#1003).
    /// Shows module name or "No module selected" in warning colors.
    /// </summary>
    private void UpdateModuleIndicator()
    {
        var moduleText = this.FindControl<TextBlock>("ModuleText");
        if (moduleText == null) return;

        try
        {
            var modulePath = RadoubSettings.Instance.CurrentModulePath;

            // Validate this is a real module path, not just the modules parent directory (#1327)
            if (!RadoubSettings.IsValidModulePath(modulePath))
            {
                moduleText.Text = "No module selected";
                moduleText.Foreground = Radoub.UI.Services.BrushManager.GetWarningBrush(this);
                return;
            }

            // Resolve .mod to working directory
            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                modulePath = FindWorkingDirectory(modulePath);

            if (string.IsNullOrEmpty(modulePath) || !Directory.Exists(modulePath))
            {
                moduleText.Text = "No module selected";
                moduleText.Foreground = Radoub.UI.Services.BrushManager.GetWarningBrush(this);
                return;
            }

            // Extract module name from module.ifo
            var ifoPath = Path.Combine(modulePath, "module.ifo");
            string? moduleName = null;
            if (File.Exists(ifoPath))
            {
                var ifo = IfoReader.Read(ifoPath);
                moduleName = ifo.ModuleName.GetDefault();
            }

            moduleText.Text = $"Module: {moduleName ?? Path.GetFileName(modulePath)}";
            moduleText.Foreground = Radoub.UI.Services.BrushManager.GetInfoBrush(this);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogUI(LogLevel.WARN, $"Failed to update module indicator: {ex.Message}");
            moduleText.Text = "No module selected";
            moduleText.Foreground = Radoub.UI.Services.BrushManager.GetWarningBrush(this);
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
            }
        }
    }

    #endregion

    #region UI Helpers

    private void UpdateTitle()
    {
        Title = _documentState.GetTitle();
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
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
