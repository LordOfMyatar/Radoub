using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ItemEditor.Services;
using ItemEditor.ViewModels;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ItemEditor.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private UtiFile? _currentItem;
    private ItemViewModel? _itemViewModel;
    private readonly DocumentState _documentState = new("ItemEditor");
    private IGameDataService? _gameDataService;
    private List<BaseItemTypeInfo>? _baseItemTypes;

    // Convenience accessors for document state
    private string? _currentFilePath
    {
        get => _documentState.CurrentFilePath;
        set => _documentState.CurrentFilePath = value;
    }
    private bool _isDirty => _documentState.IsDirty;
    private bool _isLoading
    {
        get => _documentState.IsLoading;
        set => _documentState.IsLoading = value;
    }

    public bool HasFile => _currentItem != null;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();

        // Wire up shared document state for title bar updates
        _documentState.DirtyStateChanged += () => Title = _documentState.GetTitle();

        RestoreWindowPosition();
        UpdateModuleIndicator();
        PopulateRecentFiles();

        Closing += OnWindowClosing;
        Opened += OnWindowOpened;

        UnifiedLogger.LogApplication(LogLevel.INFO, "ItemEditor MainWindow initialized");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void MarkDirty()
    {
        _documentState.MarkDirty();
    }

    // --- Window Lifecycle ---

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;

        // Initialize game data service for base item type resolution
        await InitializeGameDataAsync();

        // Handle startup file from command line
        var options = CommandLineService.Options;
        if (!string.IsNullOrEmpty(options.FilePath) && File.Exists(options.FilePath))
        {
            await OpenFileAsync(options.FilePath);
        }

        UpdateStatus("Ready");
    }

    private Task InitializeGameDataAsync()
    {
        try
        {
            _gameDataService = new GameDataService();
            if (_gameDataService.IsConfigured)
            {
                LoadBaseItemTypes();
                UnifiedLogger.LogApplication(LogLevel.INFO, "Game data service initialized");
            }
            else
            {
                LoadBaseItemTypes(); // Will use hardcoded fallback
                UnifiedLogger.LogApplication(LogLevel.WARN, "Game data service not configured, using hardcoded base item types");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to initialize game data: {ex.Message}");
            LoadBaseItemTypes();
        }

        return Task.CompletedTask;
    }

    private void LoadBaseItemTypes()
    {
        var service = new BaseItemTypeService(_gameDataService);
        _baseItemTypes = service.GetBaseItemTypes();
        PopulateBaseItemComboBox();
    }

    private void PopulateBaseItemComboBox()
    {
        BaseItemComboBox.Items.Clear();
        if (_baseItemTypes == null) return;

        foreach (var type in _baseItemTypes)
        {
            BaseItemComboBox.Items.Add(new ComboBoxItem
            {
                Content = type.DisplayName,
                Tag = type.BaseItemIndex
            });
        }
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isDirty)
        {
            e.Cancel = true;
            var result = await PromptSaveChangesAsync();
            if (result == SavePromptResult.Cancel)
                return;

            if (result == SavePromptResult.Save)
            {
                if (!await SaveCurrentFileAsync())
                    return;
            }

            _documentState.ClearDirty();
            Close();
        }

        SaveWindowPosition();
    }

    // --- File Operations ---

    private async Task<bool> OpenFileAsync(string filePath)
    {
        try
        {
            _isLoading = true;
            UpdateStatus($"Opening {Path.GetFileName(filePath)}...");

            var item = UtiReader.Read(filePath);
            _currentItem = item;
            _currentFilePath = filePath;
            _documentState.ClearDirty();

            PopulateEditor();
            OnPropertyChanged(nameof(HasFile));
            AddRecentFile(filePath);

            UpdateStatus("Ready");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened: {UnifiedLogger.SanitizePath(filePath)}");
            return true;
        }
        catch (Exception ex)
        {
            UpdateStatus("Error opening file");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open {UnifiedLogger.SanitizePath(filePath)}: {ex.Message}");
            await ShowErrorAsync($"Failed to open file:\n{ex.Message}");
            return false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<bool> SaveCurrentFileAsync()
    {
        if (_currentItem == null || string.IsNullOrEmpty(_currentFilePath))
            return false;

        try
        {
            UpdateStatus("Saving...");
            UtiWriter.Write(_currentItem, _currentFilePath);
            _documentState.ClearDirty();

            UpdateStatus("Ready");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Saved: {UnifiedLogger.SanitizePath(_currentFilePath)}");
            return true;
        }
        catch (Exception ex)
        {
            UpdateStatus("Error saving file");
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save {UnifiedLogger.SanitizePath(_currentFilePath)}: {ex.Message}");
            await ShowErrorAsync($"Failed to save file:\n{ex.Message}");
            return false;
        }
    }

    private async Task<bool> SaveAsAsync()
    {
        if (_currentItem == null)
            return false;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Item As",
            DefaultExtension = "uti",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Item Blueprint") { Patterns = new[] { "*.uti" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            },
            SuggestedFileName = Path.GetFileName(_currentFilePath ?? "item.uti")
        });

        if (file == null)
            return false;

        var path = file.Path.LocalPath;
        _currentFilePath = path;

        var result = await SaveCurrentFileAsync();
        if (result)
        {
            AddRecentFile(path);
        }
        return result;
    }

    private void PopulateEditor()
    {
        if (_currentItem == null)
        {
            EmptyStatePanel.IsVisible = true;
            EditorContent.IsVisible = false;
            ModelPartsPanel.IsVisible = false;
            ColorsPanel.IsVisible = false;
            ArmorPartsPanel.IsVisible = false;
            _itemViewModel = null;
            EditorContent.DataContext = null;
            return;
        }

        EmptyStatePanel.IsVisible = false;
        EditorContent.IsVisible = true;

        // Create ViewModel and bind
        _itemViewModel = new ItemViewModel(_currentItem);
        EditorContent.DataContext = _itemViewModel;

        // Wire up dirty tracking from ViewModel property changes
        _itemViewModel.PropertyChanged += OnItemPropertyChanged;

        // Select the correct base item type in the combo box
        SelectBaseItemInComboBox(_currentItem.BaseItem);
        UpdateConditionalFields(_currentItem.BaseItem);

        FilePathText.Text = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "";
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isLoading)
        {
            MarkDirty();
        }
    }

    private void SelectBaseItemInComboBox(int baseItemIndex)
    {
        for (int i = 0; i < BaseItemComboBox.Items.Count; i++)
        {
            if (BaseItemComboBox.Items[i] is ComboBoxItem item && item.Tag is int index && index == baseItemIndex)
            {
                _isLoading = true;
                BaseItemComboBox.SelectedIndex = i;
                _isLoading = false;
                return;
            }
        }

        // Base item not in list — add an "Unknown" entry
        var unknownItem = new ComboBoxItem
        {
            Content = $"Unknown ({baseItemIndex})",
            Tag = baseItemIndex
        };
        BaseItemComboBox.Items.Add(unknownItem);
        _isLoading = true;
        BaseItemComboBox.SelectedItem = unknownItem;
        _isLoading = false;
    }

    private void OnBaseItemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _itemViewModel == null) return;
        if (BaseItemComboBox.SelectedItem is ComboBoxItem item && item.Tag is int index)
        {
            _itemViewModel.BaseItem = index;
            UpdateConditionalFields(index);
        }
    }

    private void UpdateConditionalFields(int baseItemIndex)
    {
        var typeInfo = _baseItemTypes?.FirstOrDefault(t => t.BaseItemIndex == baseItemIndex);

        // Model Parts: show for types 0, 1, 2 (Simple, Layered, Composite)
        bool showModelParts = typeInfo?.HasModelParts ?? false;
        bool showMultipleParts = typeInfo?.HasMultipleModelParts ?? false;
        ModelPartsPanel.IsVisible = showModelParts;
        if (showModelParts)
        {
            // Parts 2 & 3 only for Composite (ModelType 2)
            ModelPart2Label.IsVisible = showMultipleParts;
            ModelPart2UpDown.IsVisible = showMultipleParts;
            ModelPart3Label.IsVisible = showMultipleParts;
            ModelPart3UpDown.IsVisible = showMultipleParts;
        }

        // Colors: show for Layered (1) and Armor (3)
        ColorsPanel.IsVisible = typeInfo?.HasColorFields ?? false;

        // Armor Parts: show for Armor (3) only
        bool showArmorParts = typeInfo?.HasArmorParts ?? false;
        ArmorPartsPanel.IsVisible = showArmorParts;
        if (showArmorParts)
        {
            PopulateArmorPartsGrid();
        }
    }

    private static readonly string[] ArmorPartNames = new[]
    {
        "Torso", "Belt", "Pelvis", "Neck", "Robe",
        "LBicep", "RBicep", "LFArm", "RFArm",
        "LHand", "RHand", "LShoul", "RShoul",
        "LThigh", "RThigh", "LShin", "RShin",
        "LFoot", "RFoot"
    };

    private void PopulateArmorPartsGrid()
    {
        if (_itemViewModel == null) return;

        ArmorPartsGrid.Children.Clear();
        ArmorPartsGrid.RowDefinitions.Clear();

        // Layout: 2 columns of label+value pairs, ~10 rows
        int row = 0;
        for (int i = 0; i < ArmorPartNames.Length; i++)
        {
            var partName = ArmorPartNames[i];
            int col = (i % 2 == 0) ? 0 : 3;
            if (i % 2 == 0)
            {
                ArmorPartsGrid.RowDefinitions.Add(new RowDefinition(Avalonia.Controls.GridLength.Auto));
            }

            var label = new TextBlock
            {
                Text = partName,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 8)
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, col);
            ArmorPartsGrid.Children.Add(label);

            var upDown = new NumericUpDown
            {
                Value = _itemViewModel.GetArmorPart(partName),
                Minimum = 0,
                Maximum = 255,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var capturedPartName = partName;
            upDown.ValueChanged += (_, args) =>
            {
                if (_isLoading || _itemViewModel == null) return;
                _itemViewModel.SetArmorPart(capturedPartName, (byte)(args.NewValue ?? 0));
            };
            Grid.SetRow(upDown, row);
            Grid.SetColumn(upDown, col + 1);
            ArmorPartsGrid.Children.Add(upDown);

            if (i % 2 == 1) row++;
        }
        // Handle odd count
        if (ArmorPartNames.Length % 2 == 1) row++;
    }

    // --- Menu Handlers ---

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var result = await PromptSaveChangesAsync();
            if (result == SavePromptResult.Cancel) return;
            if (result == SavePromptResult.Save && !await SaveCurrentFileAsync()) return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Item Blueprint",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Item Blueprint") { Patterns = new[] { "*.uti" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            await OpenFileAsync(files[0].Path.LocalPath);
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem != null && !string.IsNullOrEmpty(_currentFilePath))
        {
            await SaveCurrentFileAsync();
        }
        else if (_currentItem != null)
        {
            await SaveAsAsync();
        }
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        await SaveAsAsync();
    }

    private async void OnCloseFileClick(object? sender, RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var result = await PromptSaveChangesAsync();
            if (result == SavePromptResult.Cancel) return;
            if (result == SavePromptResult.Save && !await SaveCurrentFileAsync()) return;
        }

        // Unhook ViewModel events before clearing
        if (_itemViewModel != null)
        {
            _itemViewModel.PropertyChanged -= OnItemPropertyChanged;
        }

        _currentItem = null;
        _currentFilePath = null;
        _itemViewModel = null;
        _documentState.ClearDirty();
        PopulateEditor();
        OnPropertyChanged(nameof(HasFile));
        UpdateStatus("Ready");
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnToggleItemBrowserClick(object? sender, RoutedEventArgs e)
    {
        ItemBrowserPanel.IsVisible = !ItemBrowserPanel.IsVisible;
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        // TODO (#1706): Settings window
        UpdateStatus("Settings not yet implemented");
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = Radoub.UI.Views.AboutWindow.Create(new Radoub.UI.Views.AboutWindowConfig
        {
            ToolName = "ItemEditor",
            Version = Radoub.UI.Utils.VersionHelper.GetVersion()
        });
        aboutWindow.Show(this);
    }

    // --- Keyboard Shortcuts ---

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.O:
                    OnOpenClick(sender, e);
                    e.Handled = true;
                    break;
                case Key.S:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        OnSaveAsClick(sender, e);
                    else
                        OnSaveClick(sender, e);
                    e.Handled = true;
                    break;
            }
        }
    }

    // --- Title Bar ---

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    // --- Utility Methods ---

    private void UpdateStatus(string text)
    {
        StatusText.Text = text;
    }

    private void UpdateModuleIndicator()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (!string.IsNullOrEmpty(modulePath))
        {
            var name = Path.GetFileNameWithoutExtension(modulePath);
            ModuleIndicator.Text = $"Module: {name}";
        }
        else
        {
            ModuleIndicator.Text = "No module";
        }
    }

    private async Task<SavePromptResult> PromptSaveChangesAsync()
    {
        var dialog = new Window
        {
            Title = "Save Changes?",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var result = SavePromptResult.Cancel;

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Save changes before closing?", Margin = new Thickness(0, 0, 0, 16) });

        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };

        var saveBtn = new Button { Content = "Save" };
        saveBtn.Click += (_, _) => { result = SavePromptResult.Save; dialog.Close(); };

        var dontSaveBtn = new Button { Content = "Don't Save" };
        dontSaveBtn.Click += (_, _) => { result = SavePromptResult.DontSave; dialog.Close(); };

        var cancelBtn = new Button { Content = "Cancel" };
        cancelBtn.Click += (_, _) => { result = SavePromptResult.Cancel; dialog.Close(); };

        buttons.Children.Add(saveBtn);
        buttons.Children.Add(dontSaveBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new Window
        {
            Title = "Error",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        var okBtn = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        okBtn.Click += (_, _) => dialog.Close();
        panel.Children.Add(okBtn);

        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }

    // --- Recent Files ---

    private void PopulateRecentFiles()
    {
        RecentFilesMenu.Items.Clear();
        var recentFiles = SettingsService.Instance.RecentFiles;

        if (recentFiles.Count == 0)
        {
            var emptyItem = new MenuItem { Header = "(none)", IsEnabled = false };
            RecentFilesMenu.Items.Add(emptyItem);
            return;
        }

        foreach (var path in recentFiles)
        {
            var menuItem = new MenuItem
            {
                Header = Path.GetFileName(path),
                Tag = path
            };
            ToolTip.SetTip(menuItem, UnifiedLogger.SanitizePath(path));
            menuItem.Click += async (_, _) =>
            {
                if (_isDirty)
                {
                    var result = await PromptSaveChangesAsync();
                    if (result == SavePromptResult.Cancel) return;
                    if (result == SavePromptResult.Save && !await SaveCurrentFileAsync()) return;
                }
                await OpenFileAsync((string)menuItem.Tag!);
            };
            RecentFilesMenu.Items.Add(menuItem);
        }
    }

    private void AddRecentFile(string filePath)
    {
        SettingsService.Instance.AddRecentFile(filePath);
        PopulateRecentFiles();
    }

    // --- Window Position ---

    private void RestoreWindowPosition()
    {
        var settings = SettingsService.Instance;
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }
        if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
        {
            Position = new PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop);
        }
    }

    private void SaveWindowPosition()
    {
        var settings = SettingsService.Instance;
        if (WindowState == WindowState.Normal)
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Position.X;
            settings.WindowTop = Position.Y;
        }
    }
}

internal enum SavePromptResult
{
    Save,
    DontSave,
    Cancel
}
