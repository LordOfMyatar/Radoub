using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CreatureEditor.Services;
using Radoub.Formats.Bic;
using Radoub.Formats.Utc;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CreatureEditor.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private UtcFile? _currentCreature;
    private string? _currentFilePath;
    private bool _isDirty;
    private bool _isBicFile;

    // Bindable properties for UI state
    public bool HasFile => _currentCreature != null;
    public bool HasSelection => false; // TODO: implement
    public bool HasBackpackSelection => false; // TODO: implement
    public bool HasPaletteSelection => false; // TODO: implement
    public bool CanAddItem => HasFile && HasPaletteSelection;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        RestoreWindowPosition();

        Closing += OnWindowClosing;
        Opened += OnWindowOpened;

        UnifiedLogger.LogApplication(LogLevel.INFO, "CreatureEditor MainWindow initialized");
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;

        UpdateRecentFilesMenu();
        await HandleStartupFileAsync();
    }

    private async Task HandleStartupFileAsync()
    {
        var options = CommandLineService.Options;

        if (string.IsNullOrEmpty(options.FilePath))
            return;

        if (!File.Exists(options.FilePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Command line file not found: {UnifiedLogger.SanitizePath(options.FilePath)}");
            UpdateStatus($"File not found: {Path.GetFileName(options.FilePath)}");
            return;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading file from command line: {UnifiedLogger.SanitizePath(options.FilePath)}");
        await LoadFile(options.FilePath);
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

        // Restore panel widths
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            MainGrid.ColumnDefinitions[0].Width = new Avalonia.Controls.GridLength(settings.LeftPanelWidth);
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

        // Save panel widths
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            settings.LeftPanelWidth = MainGrid.ColumnDefinitions[0].Width.Value;
        }
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

    private void UpdateRecentFilesMenu()
    {
        RecentFilesMenu.Items.Clear();

        var recentFiles = SettingsService.Instance.RecentFiles;

        if (recentFiles.Count == 0)
        {
            var emptyItem = new MenuItem { Header = "(No recent files)", IsEnabled = false };
            RecentFilesMenu.Items.Add(emptyItem);
            return;
        }

        foreach (var filePath in recentFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var displayPath = UnifiedLogger.SanitizePath(filePath);

            var menuItem = new MenuItem
            {
                Header = fileName,
                Tag = filePath
            };
            ToolTip.SetTip(menuItem, displayPath);
            menuItem.Click += OnRecentFileClick;

            RecentFilesMenu.Items.Add(menuItem);
        }

        RecentFilesMenu.Items.Add(new Separator());

        var clearItem = new MenuItem { Header = "Clear Recent Files" };
        clearItem.Click += OnClearRecentFilesClick;
        RecentFilesMenu.Items.Add(clearItem);
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
                UpdateStatus($"File not found: {Path.GetFileName(filePath)}");
                SettingsService.Instance.RemoveRecentFile(filePath);
                UpdateRecentFilesMenu();
            }
        }
    }

    private void OnClearRecentFilesClick(object? sender, RoutedEventArgs e)
    {
        SettingsService.Instance.ClearRecentFiles();
        UpdateRecentFilesMenu();
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

    private void OnCloseFileClick(object? sender, RoutedEventArgs e)
    {
        CloseFile();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task OpenFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Creature File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Creature Files") { Patterns = new[] { "*.utc", "*.bic" } },
                new FilePickerFileType("Creature Blueprints") { Patterns = new[] { "*.utc" } },
                new FilePickerFileType("Player Characters") { Patterns = new[] { "*.bic" } },
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
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            _isBicFile = extension == ".bic";

            if (_isBicFile)
            {
                _currentCreature = BicReader.Read(filePath);
                UnifiedLogger.LogCreature(LogLevel.INFO, $"Loaded BIC (player character): {UnifiedLogger.SanitizePath(filePath)}");
            }
            else
            {
                _currentCreature = UtcReader.Read(filePath);
                UnifiedLogger.LogCreature(LogLevel.INFO, $"Loaded UTC (creature blueprint): {UnifiedLogger.SanitizePath(filePath)}");
            }

            _currentFilePath = filePath;
            _isDirty = false;

            UpdateUI();
            UpdateTitle();
            UpdateStatus($"Loaded: {Path.GetFileName(filePath)}");
            UpdateInventoryCounts();
            OnPropertyChanged(nameof(HasFile));

            SettingsService.Instance.AddRecentFile(filePath);
            UpdateRecentFilesMenu();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogCreature(LogLevel.ERROR, $"Failed to load creature: {ex.Message}");
            UpdateStatus($"Error loading file: {ex.Message}");
            await ShowErrorDialog("Load Error", $"Failed to load creature file:\n{ex.Message}");
        }
    }

    private async Task SaveFile()
    {
        if (_currentCreature == null || string.IsNullOrEmpty(_currentFilePath)) return;

        try
        {
            if (_isBicFile && _currentCreature is BicFile bicFile)
            {
                BicWriter.Write(bicFile, _currentFilePath);
            }
            else
            {
                UtcWriter.Write(_currentCreature, _currentFilePath);
            }

            _isDirty = false;
            UpdateTitle();
            UpdateStatus($"Saved: {Path.GetFileName(_currentFilePath)}");

            UnifiedLogger.LogCreature(LogLevel.INFO, $"Saved creature: {UnifiedLogger.SanitizePath(_currentFilePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogCreature(LogLevel.ERROR, $"Failed to save creature: {ex.Message}");
            UpdateStatus($"Error saving file: {ex.Message}");
            await ShowErrorDialog("Save Error", $"Failed to save creature file:\n{ex.Message}");
        }
    }

    private async Task SaveFileAs()
    {
        if (_currentCreature == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Creature As",
            DefaultExtension = _isBicFile ? ".bic" : ".utc",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Creature Blueprint") { Patterns = new[] { "*.utc" } },
                new FilePickerFileType("Player Character") { Patterns = new[] { "*.bic" } }
            }
        });

        if (file != null)
        {
            _currentFilePath = file.Path.LocalPath;
            _isBicFile = Path.GetExtension(_currentFilePath).ToLowerInvariant() == ".bic";
            await SaveFile();
            UpdateTitle();
            SettingsService.Instance.AddRecentFile(_currentFilePath);
            UpdateRecentFilesMenu();
        }
    }

    private void CloseFile()
    {
        // TODO: Check for unsaved changes
        _currentCreature = null;
        _currentFilePath = null;
        _isDirty = false;
        _isBicFile = false;

        UpdateUI();
        UpdateTitle();
        UpdateStatus("Ready");
        UpdateInventoryCounts();
        OnPropertyChanged(nameof(HasFile));
    }

    #endregion

    #region Edit Operations

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Delete selected item
        UnifiedLogger.LogInventory(LogLevel.DEBUG, "Delete clicked");
    }

    private void OnDeleteSelectedClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Delete selected backpack items
        UnifiedLogger.LogInventory(LogLevel.DEBUG, "Delete selected clicked");
    }

    private void OnAddItemClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Add item from palette
        UnifiedLogger.LogInventory(LogLevel.DEBUG, "Add item clicked");
    }

    private void OnAddToBackpackClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Add selected palette item to backpack
        UnifiedLogger.LogInventory(LogLevel.DEBUG, "Add to backpack clicked");
    }

    private void OnEquipSelectedClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Equip selected palette item
        UnifiedLogger.LogInventory(LogLevel.DEBUG, "Equip selected clicked");
    }

    #endregion

    #region UI Updates

    private void UpdateUI()
    {
        // TODO: Populate equipment slots, backpack list, and item palette
    }

    private void UpdateTitle()
    {
        var displayPath = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "Untitled";
        var dirty = _isDirty ? "*" : "";
        var fileType = _isBicFile ? " (Player)" : "";
        Title = $"Creature Editor - {displayPath}{fileType}{dirty}";
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void UpdateInventoryCounts()
    {
        if (_currentCreature == null)
        {
            InventoryCountText.Text = "";
            FilePathText.Text = "";
            return;
        }

        var equippedCount = _currentCreature.EquipItemList?.Count ?? 0;
        var backpackCount = _currentCreature.ItemList?.Count ?? 0;
        InventoryCountText.Text = $"{equippedCount} equipped, {backpackCount} in backpack";
        FilePathText.Text = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "";
    }

    private void MarkDirty()
    {
        if (!_isDirty)
        {
            _isDirty = true;
            UpdateTitle();
        }
    }

    #endregion

    #region Filter and Search

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
    }

    private void OnEquipmentTabChanged(object? sender, RoutedEventArgs e)
    {
        var showNatural = NaturalSlotsTab.IsChecked == true;
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Equipment tab changed: {(showNatural ? "Natural" : "Standard")}");
        // TODO: Update equipment slots display
    }

    #endregion

    #region Keyboard Shortcuts

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
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
            }
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            switch (e.Key)
            {
                case Key.S:
                    if (HasFile)
                    {
                        _ = SaveFileAs();
                        e.Handled = true;
                    }
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

    #region Dialogs

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Open settings window
        UpdateStatus("Settings not yet implemented");
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "About Creature Editor",
            Width = 350,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = "Creature Editor", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.Bold });
        panel.Children.Add(new TextBlock { Text = "Creature and Inventory Editor" });
        panel.Children.Add(new TextBlock { Text = "for Neverwinter Nights" });
        panel.Children.Add(new TextBlock { Text = "Version 0.1.0-alpha" });
        panel.Children.Add(new TextBlock { Text = "Part of the Radoub Toolset", Margin = new Avalonia.Thickness(0, 10, 0, 0) });

        var button = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        dialog.Show(this);
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

    #endregion

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
