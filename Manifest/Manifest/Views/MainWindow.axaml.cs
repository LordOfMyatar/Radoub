using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Manifest.Services;
using Radoub.Formats.Jrl;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Manifest.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private JrlFile? _currentJrl;
    private string? _currentFilePath;
    private bool _isDirty;
    private object? _selectedItem;

    // Bindable properties for UI state
    public bool HasFile => _currentJrl != null;
    public bool HasSelection => _selectedItem != null;
    public bool CanAddEntry => HasFile && (_selectedItem is CategoryTreeItem || _selectedItem is EntryTreeItem);

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Restore window position
        RestoreWindowPosition();

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

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        await OpenFile();
    }

    private async void OnOpenFromModuleClick(object? sender, RoutedEventArgs e)
    {
        await OpenFromModule();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        await SaveFile();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
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

    private async Task OpenFromModule()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Module Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var folder = folders[0];
            var modulePath = folder.Path.LocalPath;
            var jrlPath = Path.Combine(modulePath, "module.jrl");

            if (File.Exists(jrlPath))
            {
                await LoadFile(jrlPath);
                UnifiedLogger.LogJournal(LogLevel.INFO, $"Opened module.jrl from: {UnifiedLogger.SanitizePath(modulePath)}");
            }
            else
            {
                UpdateStatus("No module.jrl found in selected folder");
                await ShowErrorDialog("File Not Found", $"No module.jrl file found in:\n{UnifiedLogger.SanitizePath(modulePath)}");
            }
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
        if (_currentJrl == null || string.IsNullOrEmpty(_currentFilePath)) return;

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

        // Select the new category and focus the name field
        SelectNewCategory(newCategory);

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

        // Auto-increment ID by 100 (allows inserting entries between)
        uint nextId = category.Entries.Count > 0
            ? ((category.Entries.Max(e => e.ID) / 100) + 1) * 100
            : 100;

        var entryText = new JrlLocString();
        entryText.SetString(0, "");

        var newEntry = new JournalEntry
        {
            ID = nextId,
            Text = entryText,
            End = false
        };

        category.Entries.Add(newEntry);
        MarkDirty();
        UpdateTree();

        // Select the new entry and focus the text field
        SelectNewEntry(newEntry, category);

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

    private void SelectNewCategory(JournalCategory category)
    {
        // Find and select the tree item for this category
        foreach (var treeItem in JournalTree.Items.OfType<TreeViewItem>())
        {
            if (treeItem.Tag is CategoryTreeItem catItem && catItem.Category == category)
            {
                JournalTree.SelectedItem = treeItem;
                treeItem.Focus();
                // Focus the name box after a brief delay to let UI update
                Avalonia.Threading.Dispatcher.UIThread.Post(() => CategoryNameBox.Focus());
                return;
            }
        }
    }

    private void SelectNewEntry(JournalEntry entry, JournalCategory category)
    {
        // Find and select the tree item for this entry
        foreach (var catTreeItem in JournalTree.Items.OfType<TreeViewItem>())
        {
            if (catTreeItem.Tag is CategoryTreeItem catItem && catItem.Category == category)
            {
                foreach (var entTreeItem in catTreeItem.Items.OfType<TreeViewItem>())
                {
                    if (entTreeItem.Tag is EntryTreeItem entItem && entItem.Entry == entry)
                    {
                        JournalTree.SelectedItem = entTreeItem;
                        entTreeItem.Focus();
                        // Focus the text box after a brief delay to let UI update
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => EntryTextBox.Focus());
                        return;
                    }
                }
            }
        }
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

    private bool _isUpdatingPanel = false;

    private void UpdatePropertyPanel()
    {
        _isUpdatingPanel = true;
        try
        {
            CategoryProperties.IsVisible = false;
            EntryProperties.IsVisible = false;
            NoSelectionText.IsVisible = true;

            if (_selectedItem is CategoryTreeItem catItem)
            {
                CategoryProperties.IsVisible = true;
                NoSelectionText.IsVisible = false;

                CategoryTagBox.Text = catItem.Category.Tag;
                CategoryStrRefBox.Text = FormatStrRef(catItem.Category.Name.StrRef);
                CategoryNameBox.Text = catItem.Category.Name.GetDefault();
                SelectPriorityItem(catItem.Category.Priority);
                CategoryXPBox.Value = catItem.Category.XP;
                CategoryCommentBox.Text = catItem.Category.Comment;

                // Wire up change handlers
                WireCategoryHandlers();
            }
            else if (_selectedItem is EntryTreeItem entItem)
            {
                EntryProperties.IsVisible = true;
                NoSelectionText.IsVisible = false;

                EntryIdBox.Value = entItem.Entry.ID;
                EntryEndBox.IsChecked = entItem.Entry.End;
                EntryStrRefBox.Text = FormatStrRef(entItem.Entry.Text.StrRef);
                EntryTextBox.Text = entItem.Entry.Text.GetDefault();

                // Wire up change handlers
                WireEntryHandlers();
            }
        }
        finally
        {
            _isUpdatingPanel = false;
        }
    }

    private static string FormatStrRef(uint strRef)
    {
        if (strRef == 0xFFFFFFFF)
            return "(none)";
        return $"{strRef} (0x{strRef:X8})";
    }

    private void SelectPriorityItem(uint priority)
    {
        foreach (var obj in CategoryPriorityBox.Items)
        {
            if (obj is ComboBoxItem item && item.Tag is string tagStr &&
                uint.TryParse(tagStr, out var tagVal) && tagVal == priority)
            {
                CategoryPriorityBox.SelectedItem = item;
                return;
            }
        }
        // Default to Medium (2) if not found
        CategoryPriorityBox.SelectedIndex = 2;
    }

    private void WireCategoryHandlers()
    {
        // Remove old handlers to prevent duplicates
        CategoryNameBox.LostFocus -= OnCategoryNameChanged;
        CategoryTagBox.LostFocus -= OnCategoryTagChanged;
        CategoryPriorityBox.SelectionChanged -= OnCategoryPriorityChanged;
        CategoryXPBox.ValueChanged -= OnCategoryXPChanged;
        CategoryCommentBox.LostFocus -= OnCategoryCommentChanged;

        // Add handlers
        CategoryNameBox.LostFocus += OnCategoryNameChanged;
        CategoryTagBox.LostFocus += OnCategoryTagChanged;
        CategoryPriorityBox.SelectionChanged += OnCategoryPriorityChanged;
        CategoryXPBox.ValueChanged += OnCategoryXPChanged;
        CategoryCommentBox.LostFocus += OnCategoryCommentChanged;
    }

    private void WireEntryHandlers()
    {
        // Remove old handlers to prevent duplicates
        EntryIdBox.ValueChanged -= OnEntryIdChanged;
        EntryEndBox.IsCheckedChanged -= OnEntryEndChanged;
        EntryTextBox.LostFocus -= OnEntryTextChanged;

        // Add handlers
        EntryIdBox.ValueChanged += OnEntryIdChanged;
        EntryEndBox.IsCheckedChanged += OnEntryEndChanged;
        EntryTextBox.LostFocus += OnEntryTextChanged;
    }

    private void OnCategoryNameChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        var newName = CategoryNameBox.Text ?? "";
        if (catItem.Category.Name.GetDefault() != newName)
        {
            catItem.Category.Name.SetString(0, newName);
            MarkDirty();
            UpdateTreeItemHeader(catItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Category name changed to: {newName}");
        }
    }

    private void OnCategoryTagChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        var newTag = CategoryTagBox.Text ?? "";
        if (catItem.Category.Tag != newTag)
        {
            catItem.Category.Tag = newTag;
            MarkDirty();
            UpdateTreeItemHeader(catItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Category tag changed to: {newTag}");
        }
    }

    private void OnCategoryPriorityChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        if (CategoryPriorityBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr && uint.TryParse(tagStr, out var newPriority))
        {
            if (catItem.Category.Priority != newPriority)
            {
                catItem.Category.Priority = newPriority;
                MarkDirty();
                UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Category priority changed to: {newPriority}");
            }
        }
    }

    private void OnCategoryXPChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        var newXP = (uint)(CategoryXPBox.Value ?? 0);
        if (catItem.Category.XP != newXP)
        {
            catItem.Category.XP = newXP;
            MarkDirty();
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Category XP changed to: {newXP}");
        }
    }

    private void OnCategoryCommentChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not CategoryTreeItem catItem) return;

        var newComment = CategoryCommentBox.Text ?? "";
        if (catItem.Category.Comment != newComment)
        {
            catItem.Category.Comment = newComment;
            MarkDirty();
            UnifiedLogger.LogJournal(LogLevel.DEBUG, "Category comment changed");
        }
    }

    private void OnEntryIdChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not EntryTreeItem entItem) return;

        var newId = (uint)(EntryIdBox.Value ?? 0);
        if (entItem.Entry.ID != newId)
        {
            entItem.Entry.ID = newId;
            MarkDirty();
            UpdateTreeItemHeader(entItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Entry ID changed to: {newId}");
        }
    }

    private void OnEntryEndChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not EntryTreeItem entItem) return;

        var newEnd = EntryEndBox.IsChecked ?? false;
        if (entItem.Entry.End != newEnd)
        {
            entItem.Entry.End = newEnd;
            MarkDirty();
            UpdateTreeItemHeader(entItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Entry End changed to: {newEnd}");
        }
    }

    private void OnEntryTextChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanel || _selectedItem is not EntryTreeItem entItem) return;

        var newText = EntryTextBox.Text ?? "";
        var oldText = entItem.Entry.Text.GetDefault();
        if (oldText != newText)
        {
            entItem.Entry.Text.SetString(0, newText);
            MarkDirty();
            UpdateTreeItemHeader(entItem);
            UnifiedLogger.LogJournal(LogLevel.DEBUG, "Entry text changed");
        }
    }

    private void UpdateTreeItemHeader(object item)
    {
        // Find and update the tree item header
        foreach (var treeItem in JournalTree.Items.OfType<TreeViewItem>())
        {
            if (treeItem.Tag == item)
            {
                if (item is CategoryTreeItem catItem)
                    treeItem.Header = catItem.DisplayName;
                return;
            }

            // Check children for entry items
            foreach (var childItem in treeItem.Items.OfType<TreeViewItem>())
            {
                if (childItem.Tag == item && item is EntryTreeItem entItem)
                {
                    childItem.Header = entItem.DisplayName;
                    return;
                }
            }
        }
    }

    #endregion

    #region UI Helpers

    private void UpdateTitle()
    {
        var displayPath = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "Untitled";
        var dirty = _isDirty ? "*" : "";
        Title = $"Manifest - {displayPath}{dirty}";
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
