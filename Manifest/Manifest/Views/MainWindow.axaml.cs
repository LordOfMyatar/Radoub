using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Manifest.Services;
using Radoub.Formats.Jrl;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Manifest.Views;

public partial class MainWindow : Window
{
    private JrlFile? _currentJrl;
    private string? _currentFilePath;
    private bool _isDirty;
    private object? _selectedItem;

    // Bindable properties for UI state
    public bool HasFile => _currentJrl != null;
    public bool HasSelection => _selectedItem != null;
    public bool CanAddEntry => HasFile && (_selectedItem is CategoryTreeItem || _selectedItem is EntryTreeItem);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Restore window position
        RestoreWindowPosition();

        // Set up recent files menu
        UpdateRecentFilesMenu();

        // Subscribe to settings changes
        SettingsService.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.RecentFiles))
            {
                UpdateRecentFilesMenu();
            }
        };

        // Handle window closing
        Closing += OnWindowClosing;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Manifest MainWindow initialized");
    }

    private void RestoreWindowPosition()
    {
        var settings = SettingsService.Instance;
        Position = new Avalonia.PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop);
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveWindowPosition()
    {
        var settings = SettingsService.Instance;

        if (WindowState == WindowState.Normal)
        {
            settings.WindowLeft = Position.X;
            settings.WindowTop = Position.Y;
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
        }
        settings.WindowMaximized = WindowState == WindowState.Maximized;
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isDirty)
        {
            e.Cancel = true;
            var result = await ShowUnsavedChangesDialog();
            if (result == "Save")
            {
                await SaveFile();
                Close();
            }
            else if (result == "Discard")
            {
                _isDirty = false;
                Close();
            }
            // else Cancel - do nothing, stay open
        }
        else
        {
            SaveWindowPosition();
        }
    }

    private async Task<string> ShowUnsavedChangesDialog()
    {
        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var result = "Cancel";

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock { Text = "You have unsaved changes. What would you like to do?" });

        var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };

        var saveButton = new Button { Content = "Save" };
        saveButton.Click += (s, e) => { result = "Save"; dialog.Close(); };

        var discardButton = new Button { Content = "Discard" };
        discardButton.Click += (s, e) => { result = "Discard"; dialog.Close(); };

        var cancelButton = new Button { Content = "Cancel" };
        cancelButton.Click += (s, e) => { result = "Cancel"; dialog.Close(); };

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(discardButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(this);

        return result;
    }

    #region File Operations

    private void OnNewClick(object? sender, RoutedEventArgs e)
    {
        CreateNewJournal();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        await OpenFile();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        await SaveFile();
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        await SaveFileAs();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CreateNewJournal()
    {
        _currentJrl = new JrlFile();
        _currentFilePath = null;
        _isDirty = false;

        UpdateTree();
        UpdateTitle();
        UpdateStatus("New journal created");
        OnPropertyChanged(nameof(HasFile));

        UnifiedLogger.LogJournal(LogLevel.INFO, "Created new journal");
    }

    private async Task OpenFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Journal File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Journal Files") { Patterns = new[] { "*.jrl" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var file = files[0];
            await LoadFile(file.Path.LocalPath);
        }
    }

    private async Task LoadFile(string filePath)
    {
        try
        {
            _currentJrl = JrlReader.Read(filePath);
            _currentFilePath = filePath;
            _isDirty = false;

            UpdateTree();
            UpdateTitle();
            UpdateStatus($"Loaded: {Path.GetFileName(filePath)}");
            OnPropertyChanged(nameof(HasFile));

            // Add to recent files
            SettingsService.Instance.AddRecentFile(filePath);

            UnifiedLogger.LogJournal(LogLevel.INFO, $"Loaded journal: {UnifiedLogger.SanitizePath(filePath)} ({_currentJrl.Categories.Count} categories)");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogJournal(LogLevel.ERROR, $"Failed to load journal: {ex.Message}");
            UpdateStatus($"Error loading file: {ex.Message}");
            await ShowErrorDialog("Load Error", $"Failed to load journal file:\n{ex.Message}");
        }
    }

    private async Task SaveFile()
    {
        if (_currentJrl == null) return;

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            await SaveFileAs();
            return;
        }

        try
        {
            JrlWriter.Write(_currentJrl, _currentFilePath);
            _isDirty = false;
            UpdateTitle();
            UpdateStatus($"Saved: {Path.GetFileName(_currentFilePath)}");

            UnifiedLogger.LogJournal(LogLevel.INFO, $"Saved journal: {UnifiedLogger.SanitizePath(_currentFilePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogJournal(LogLevel.ERROR, $"Failed to save journal: {ex.Message}");
            UpdateStatus($"Error saving file: {ex.Message}");
            await ShowErrorDialog("Save Error", $"Failed to save journal file:\n{ex.Message}");
        }
    }

    private async Task SaveFileAs()
    {
        if (_currentJrl == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Journal File",
            DefaultExtension = "jrl",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Journal Files") { Patterns = new[] { "*.jrl" } }
            },
            SuggestedFileName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "journal.jrl"
        });

        if (file != null)
        {
            _currentFilePath = file.Path.LocalPath;
            await SaveFile();
            SettingsService.Instance.AddRecentFile(_currentFilePath);
        }
    }

    #endregion

    #region Recent Files

    private void UpdateRecentFilesMenu()
    {
        var recentFiles = SettingsService.Instance.RecentFiles;

        // Clear existing items except the "No recent files" item
        while (RecentFilesMenu.Items.Count > 1)
        {
            RecentFilesMenu.Items.RemoveAt(0);
        }

        if (recentFiles.Count == 0)
        {
            NoRecentFilesItem.IsVisible = true;
        }
        else
        {
            NoRecentFilesItem.IsVisible = false;

            // Insert before the "No recent files" item
            for (int i = recentFiles.Count - 1; i >= 0; i--)
            {
                var filePath = recentFiles[i];
                var menuItem = new MenuItem
                {
                    Header = $"_{i + 1}. {Path.GetFileName(filePath)}",
                    Tag = filePath
                };
                menuItem.Click += OnRecentFileClick;
                RecentFilesMenu.Items.Insert(0, menuItem);
            }
        }
    }

    private async void OnRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
        {
            if (File.Exists(filePath))
            {
                await LoadFile(filePath);
            }
            else
            {
                await ShowErrorDialog("File Not Found", $"The file no longer exists:\n{filePath}");
                SettingsService.Instance.RemoveRecentFile(filePath);
            }
        }
    }

    private void OnClearRecentFilesClick(object? sender, RoutedEventArgs e)
    {
        SettingsService.Instance.ClearRecentFiles();
    }

    #endregion

    #region Edit Operations

    private void OnAddCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (_currentJrl == null) return;

        var name = new JrlLocString();
        name.SetString(0, "New Category");

        var newCategory = new JournalCategory
        {
            Name = name,
            Tag = "new_category",
            Priority = 1,
            XP = 0,
            Comment = ""
        };

        _currentJrl.Categories.Add(newCategory);
        MarkDirty();
        UpdateTree();

        UnifiedLogger.LogJournal(LogLevel.INFO, "Added new category");
    }

    private void OnAddEntryClick(object? sender, RoutedEventArgs e)
    {
        if (_currentJrl == null) return;

        JournalCategory? category = null;

        if (_selectedItem is CategoryTreeItem catItem)
        {
            category = catItem.Category;
        }
        else if (_selectedItem is EntryTreeItem entItem)
        {
            category = entItem.ParentCategory;
        }

        if (category == null) return;

        // Auto-increment ID
        uint nextId = category.Entries.Count > 0
            ? category.Entries.Max(e => e.ID) + 1
            : 1;

        var entryText = new JrlLocString();
        entryText.SetString(0, "New entry");

        var newEntry = new JournalEntry
        {
            ID = nextId,
            Text = entryText,
            End = false
        };

        category.Entries.Add(newEntry);
        MarkDirty();
        UpdateTree();

        UnifiedLogger.LogJournal(LogLevel.INFO, $"Added new entry with ID {nextId}");
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_currentJrl == null || _selectedItem == null) return;

        if (_selectedItem is CategoryTreeItem catItem)
        {
            _currentJrl.Categories.Remove(catItem.Category);
            UnifiedLogger.LogJournal(LogLevel.INFO, $"Deleted category: {catItem.Category.Tag}");
        }
        else if (_selectedItem is EntryTreeItem entItem)
        {
            entItem.ParentCategory.Entries.Remove(entItem.Entry);
            UnifiedLogger.LogJournal(LogLevel.INFO, $"Deleted entry: {entItem.Entry.ID}");
        }

        _selectedItem = null;
        MarkDirty();
        UpdateTree();
        UpdatePropertyPanel();
    }

    #endregion

    #region Tree View

    private void UpdateTree()
    {
        JournalTree.Items.Clear();

        if (_currentJrl == null) return;

        foreach (var category in _currentJrl.Categories)
        {
            var catItem = new CategoryTreeItem(category);

            var catNode = new TreeViewItem
            {
                Header = catItem.DisplayName,
                Tag = catItem,
                IsExpanded = true
            };

            foreach (var entry in category.Entries)
            {
                var entItem = new EntryTreeItem(entry, category);
                var entNode = new TreeViewItem
                {
                    Header = entItem.DisplayName,
                    Tag = entItem
                };
                catNode.Items.Add(entNode);
            }

            JournalTree.Items.Add(catNode);
        }
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (JournalTree.SelectedItem is TreeViewItem treeItem)
        {
            _selectedItem = treeItem.Tag;
        }
        else
        {
            _selectedItem = null;
        }

        UpdatePropertyPanel();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanAddEntry));
    }

    #endregion

    #region Property Panel

    private void UpdatePropertyPanel()
    {
        CategoryProperties.IsVisible = false;
        EntryProperties.IsVisible = false;
        NoSelectionText.IsVisible = true;

        if (_selectedItem is CategoryTreeItem catItem)
        {
            CategoryProperties.IsVisible = true;
            NoSelectionText.IsVisible = false;

            CategoryNameBox.Text = catItem.Category.Name.GetDefault();
            CategoryTagBox.Text = catItem.Category.Tag;
            CategoryPriorityBox.Value = catItem.Category.Priority;
            CategoryXPBox.Value = catItem.Category.XP;
            CategoryCommentBox.Text = catItem.Category.Comment;
        }
        else if (_selectedItem is EntryTreeItem entItem)
        {
            EntryProperties.IsVisible = true;
            NoSelectionText.IsVisible = false;

            EntryIdBox.Value = entItem.Entry.ID;
            EntryEndBox.IsChecked = entItem.Entry.End;
            EntryTextBox.Text = entItem.Entry.Text.GetDefault();
        }
    }

    #endregion

    #region UI Helpers

    private void UpdateTitle()
    {
        var fileName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "Untitled";
        var dirty = _isDirty ? "*" : "";
        Title = $"Manifest - {fileName}{dirty}";
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void MarkDirty()
    {
        if (!_isDirty)
        {
            _isDirty = true;
            UpdateTitle();
        }
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        var button = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "About Manifest",
            Width = 350,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = "Manifest", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.Bold });
        panel.Children.Add(new TextBlock { Text = "Journal Editor for Neverwinter Nights" });
        panel.Children.Add(new TextBlock { Text = "Version 0.1.0-alpha" });
        panel.Children.Add(new TextBlock { Text = "Part of the Radoub Toolset", Margin = new Avalonia.Thickness(0, 10, 0, 0) });

        var button = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        dialog.Show(this);
    }

    private void OnPropertyChanged(string propertyName)
    {
        // Notify binding system of property changes
        // Using simple invalidation approach for now
    }

    #endregion
}

#region Tree Item Models

internal class CategoryTreeItem
{
    public JournalCategory Category { get; }
    public string DisplayName => $"[{Category.Tag}] {Category.Name.GetDefault()}";

    public CategoryTreeItem(JournalCategory category)
    {
        Category = category;
    }
}

internal class EntryTreeItem
{
    public JournalEntry Entry { get; }
    public JournalCategory ParentCategory { get; }
    public string DisplayName => $"[{Entry.ID}] {(Entry.End ? "(END) " : "")}{TruncateText(Entry.Text.GetDefault(), 40)}";

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
