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
using System.Collections.ObjectModel;
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
    private List<PaletteCategory> _paletteCategories = new();
    private ItemPropertyService? _itemPropertyService;
    private ItemStatisticsService? _itemStatisticsService;
    private PropertyTypeInfo? _selectedPropertyType;
    private int _editingPropertyIndex = -1; // -1 = add mode, >= 0 = editing that index

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
                InitializePropertyServices();
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

        InitializePropertySearchHandler();
        return Task.CompletedTask;
    }

    private void InitializePropertyServices()
    {
        if (_gameDataService == null) return;

        _itemPropertyService = new ItemPropertyService(_gameDataService);
        _itemStatisticsService = new ItemStatisticsService(_itemPropertyService);
        PopulateAvailableProperties();
    }

    private void LoadBaseItemTypes()
    {
        var service = new BaseItemTypeService(_gameDataService);
        _baseItemTypes = service.GetBaseItemTypes();
        PopulateBaseItemComboBox();
        LoadPaletteCategories();
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

    private void LoadPaletteCategories()
    {
        _paletteCategories.Clear();
        PaletteCategoryComboBox.Items.Clear();

        if (_gameDataService != null && _gameDataService.IsConfigured)
        {
            try
            {
                var categories = _gameDataService.GetPaletteCategories(Radoub.Formats.Common.ResourceTypes.Uti).ToList();
                _paletteCategories = categories;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load palette categories: {ex.Message}");
            }
        }

        // Hardcoded fallback if no categories loaded
        if (_paletteCategories.Count == 0)
        {
            _paletteCategories = GetHardcodedPaletteCategories();
        }

        foreach (var cat in _paletteCategories)
        {
            PaletteCategoryComboBox.Items.Add(new ComboBoxItem
            {
                Content = cat.Name,
                Tag = cat.Id
            });
        }
    }

    private static List<PaletteCategory> GetHardcodedPaletteCategories()
    {
        return new List<PaletteCategory>
        {
            new() { Id = 0, Name = "Miscellaneous" },
            new() { Id = 1, Name = "Armor" },
            new() { Id = 2, Name = "Weapons" },
            new() { Id = 3, Name = "Potions" },
            new() { Id = 4, Name = "Other" },
        };
    }

    private void SelectPaletteCategoryInComboBox(byte paletteId)
    {
        for (int i = 0; i < PaletteCategoryComboBox.Items.Count; i++)
        {
            if (PaletteCategoryComboBox.Items[i] is ComboBoxItem item && item.Tag is byte id && id == paletteId)
            {
                _isLoading = true;
                PaletteCategoryComboBox.SelectedIndex = i;
                _isLoading = false;
                return;
            }
        }
        // Not found — select first item if available
        if (PaletteCategoryComboBox.Items.Count > 0)
        {
            _isLoading = true;
            PaletteCategoryComboBox.SelectedIndex = 0;
            _isLoading = false;
        }
    }

    private void OnPaletteCategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _itemViewModel == null) return;
        if (PaletteCategoryComboBox.SelectedItem is ComboBoxItem item && item.Tag is byte id)
        {
            _itemViewModel.PaletteID = id;
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
            PropertyConfigPanel.IsVisible = false;
            AssignedPropertiesList.Items.Clear();
            _selectedPropertyType = null;
            _editingPropertyIndex = -1;
            AddPropertyButton.IsEnabled = false;
            EditPropertyButton.IsEnabled = false;
            RemovePropertyButton.IsEnabled = false;
            ClearAllPropertiesButton.IsEnabled = false;
            ItemStatisticsPanel.IsVisible = false;
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

        // Select the correct base item type and palette category
        SelectBaseItemInComboBox(_currentItem.BaseItem);
        UpdateConditionalFields(_currentItem.BaseItem);
        SelectPaletteCategoryInComboBox(_currentItem.PaletteID);

        // Populate assigned properties list
        RefreshAssignedProperties();

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

    // --- Item Properties UI ---

    private void InitializePropertySearchHandler()
    {
        PropertySearchBox.TextChanged += OnPropertySearchTextChanged;
    }

    private void PopulateAvailableProperties(string? searchFilter = null)
    {
        AvailablePropertiesTree.Items.Clear();

        if (_itemPropertyService == null)
            return;

        var types = string.IsNullOrWhiteSpace(searchFilter)
            ? _itemPropertyService.GetAvailablePropertyTypes()
            : _itemPropertyService.SearchProperties(searchFilter);

        foreach (var type in types)
        {
            var node = new TreeViewItem
            {
                Header = type.DisplayName,
                Tag = type
            };

            // Add subtypes as children if this property has them
            if (type.HasSubtypes)
            {
                var subtypes = _itemPropertyService.GetSubtypes(type.PropertyIndex);
                foreach (var subtype in subtypes)
                {
                    node.Items.Add(new TreeViewItem
                    {
                        Header = subtype.DisplayName,
                        Tag = subtype
                    });
                }
            }

            AvailablePropertiesTree.Items.Add(node);
        }
    }

    private void OnPropertySearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var filter = PropertySearchBox.Text;
        PopulateAvailableProperties(filter);
    }

    private void OnAvailablePropertySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Selecting from Available tree exits edit mode
        _editingPropertyIndex = -1;
        ApplyEditButton.IsVisible = false;

        if (AvailablePropertiesTree.SelectedItem is TreeViewItem selectedNode)
        {
            PropertyTypeInfo? propertyType = null;

            if (selectedNode.Tag is PropertyTypeInfo type)
            {
                propertyType = type;
            }
            else if (selectedNode.Tag is TwoDAEntry && selectedNode.Parent is TreeViewItem parentNode && parentNode.Tag is PropertyTypeInfo parentType)
            {
                propertyType = parentType;
                // Auto-select the subtype in dropdown
            }

            if (propertyType != null)
            {
                _selectedPropertyType = propertyType;
                UpdatePropertyConfigPanel(propertyType);
                AddPropertyButton.IsEnabled = _currentItem != null;

                // If a subtype child node was selected, pre-select it in the dropdown
                if (selectedNode.Tag is TwoDAEntry subtypeEntry && SubtypeComboBox.IsVisible)
                {
                    for (int i = 0; i < SubtypeComboBox.Items.Count; i++)
                    {
                        if (SubtypeComboBox.Items[i] is ComboBoxItem item && item.Tag is int idx && idx == subtypeEntry.Index)
                        {
                            SubtypeComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            _selectedPropertyType = null;
            PropertyConfigPanel.IsVisible = false;
            AddPropertyButton.IsEnabled = false;
        }
    }

    private void UpdatePropertyConfigPanel(PropertyTypeInfo propertyType)
    {
        PropertyConfigPanel.IsVisible = true;
        SelectedPropertyName.Text = propertyType.DisplayName;

        // Subtypes
        bool hasSubtypes = propertyType.HasSubtypes;
        SubtypeLabel.IsVisible = hasSubtypes;
        SubtypeComboBox.IsVisible = hasSubtypes;
        SubtypeComboBox.Items.Clear();
        if (hasSubtypes && _itemPropertyService != null)
        {
            var subtypes = _itemPropertyService.GetSubtypes(propertyType.PropertyIndex);
            foreach (var sub in subtypes)
            {
                SubtypeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = sub.DisplayName,
                    Tag = sub.Index
                });
            }
            if (SubtypeComboBox.Items.Count > 0)
                SubtypeComboBox.SelectedIndex = 0;
        }

        // Cost values
        bool hasCost = propertyType.HasCostTable;
        CostValueLabel.IsVisible = hasCost;
        CostValueComboBox.IsVisible = hasCost;
        CostValueComboBox.Items.Clear();
        if (hasCost && _itemPropertyService != null)
        {
            var costValues = _itemPropertyService.GetCostValues(propertyType.PropertyIndex);
            foreach (var cost in costValues)
            {
                CostValueComboBox.Items.Add(new ComboBoxItem
                {
                    Content = cost.DisplayName,
                    Tag = cost.Index
                });
            }
            if (CostValueComboBox.Items.Count > 0)
                CostValueComboBox.SelectedIndex = 0;
        }

        // Param values
        bool hasParam = propertyType.HasParamTable;
        ParamValueLabel.IsVisible = hasParam;
        ParamValueComboBox.IsVisible = hasParam;
        ParamValueComboBox.Items.Clear();
        if (hasParam && _itemPropertyService != null)
        {
            var paramValues = _itemPropertyService.GetParamValues(propertyType.PropertyIndex);
            foreach (var param in paramValues)
            {
                ParamValueComboBox.Items.Add(new ComboBoxItem
                {
                    Content = param.DisplayName,
                    Tag = param.Index
                });
            }
            if (ParamValueComboBox.Items.Count > 0)
                ParamValueComboBox.SelectedIndex = 0;
        }
    }

    private void OnAddPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null || _selectedPropertyType == null)
            return;

        int subtypeIndex = 0;
        if (SubtypeComboBox.IsVisible && SubtypeComboBox.SelectedItem is ComboBoxItem subItem && subItem.Tag is int subIdx)
            subtypeIndex = subIdx;

        int costValueIndex = 0;
        if (CostValueComboBox.IsVisible && CostValueComboBox.SelectedItem is ComboBoxItem costItem && costItem.Tag is int costIdx)
            costValueIndex = costIdx;

        int? paramValueIndex = null;
        if (ParamValueComboBox.IsVisible && ParamValueComboBox.SelectedItem is ComboBoxItem paramItem && paramItem.Tag is int paramIdx)
            paramValueIndex = paramIdx;

        var property = _itemPropertyService.CreateItemProperty(
            _selectedPropertyType.PropertyIndex,
            subtypeIndex,
            costValueIndex,
            paramValueIndex);

        _currentItem.Properties.Add(property);
        RefreshAssignedProperties();
        MarkDirty();

        UpdateStatus($"Added property: {_selectedPropertyType.DisplayName}");
    }

    private void OnRemovePropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null)
            return;

        // Get all selected indices, sorted descending so removal doesn't shift indices
        var selectedIndices = AssignedPropertiesList.Selection.SelectedIndexes
            .Where(i => i >= 0 && i < _currentItem.Properties.Count)
            .OrderByDescending(i => i)
            .ToList();

        if (selectedIndices.Count == 0)
            return;

        foreach (var index in selectedIndices)
        {
            _currentItem.Properties.RemoveAt(index);
        }

        RefreshAssignedProperties();
        MarkDirty();

        var count = selectedIndices.Count;
        UpdateStatus(count == 1 ? "Property removed" : $"{count} properties removed");
    }

    private void OnClearAllPropertiesClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _currentItem.Properties.Count == 0)
            return;

        var count = _currentItem.Properties.Count;
        _currentItem.Properties.Clear();
        RefreshAssignedProperties();
        MarkDirty();
        UpdateStatus($"Cleared {count} properties");
    }

    private void OnAssignedPropertySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedCount = AssignedPropertiesList.Selection.SelectedIndexes.Count();
        bool hasSelection = selectedCount > 0;
        RemovePropertyButton.IsEnabled = hasSelection;
        // Edit only enabled for single selection
        EditPropertyButton.IsEnabled = selectedCount == 1;
    }

    private void OnEditPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null || AssignedPropertiesList.SelectedIndex < 0)
            return;

        var index = AssignedPropertiesList.SelectedIndex;
        if (index >= _currentItem.Properties.Count)
            return;

        var prop = _currentItem.Properties[index];
        _editingPropertyIndex = index;

        // Find the property type info
        var types = _itemPropertyService.GetAvailablePropertyTypes();
        var type = types.FirstOrDefault(t => t.PropertyIndex == prop.PropertyName);
        if (type == null)
        {
            UpdateStatus($"Unknown property type: {prop.PropertyName}");
            return;
        }

        _selectedPropertyType = type;
        UpdatePropertyConfigPanel(type);

        // Pre-select current values in dropdowns
        SelectComboBoxByTag(SubtypeComboBox, prop.Subtype);
        SelectComboBoxByTag(CostValueComboBox, prop.CostValue);
        if (prop.Param1 != 0xFF)
            SelectComboBoxByTag(ParamValueComboBox, prop.Param1Value);

        // Show Apply button, hide Add
        ApplyEditButton.IsVisible = true;
        AddPropertyButton.IsEnabled = false;

        UpdateStatus($"Editing: {type.DisplayName}");
    }

    private void OnApplyEditClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _itemPropertyService == null || _selectedPropertyType == null)
            return;

        if (_editingPropertyIndex < 0 || _editingPropertyIndex >= _currentItem.Properties.Count)
            return;

        int subtypeIndex = 0;
        if (SubtypeComboBox.IsVisible && SubtypeComboBox.SelectedItem is ComboBoxItem subItem && subItem.Tag is int subIdx)
            subtypeIndex = subIdx;

        int costValueIndex = 0;
        if (CostValueComboBox.IsVisible && CostValueComboBox.SelectedItem is ComboBoxItem costItem && costItem.Tag is int costIdx)
            costValueIndex = costIdx;

        int? paramValueIndex = null;
        if (ParamValueComboBox.IsVisible && ParamValueComboBox.SelectedItem is ComboBoxItem paramItem && paramItem.Tag is int paramIdx)
            paramValueIndex = paramIdx;

        var property = _itemPropertyService.CreateItemProperty(
            _selectedPropertyType.PropertyIndex,
            subtypeIndex,
            costValueIndex,
            paramValueIndex);

        _currentItem.Properties[_editingPropertyIndex] = property;
        RefreshAssignedProperties();
        MarkDirty();

        // Exit edit mode
        _editingPropertyIndex = -1;
        ApplyEditButton.IsVisible = false;
        PropertyConfigPanel.IsVisible = false;

        UpdateStatus($"Updated property: {_selectedPropertyType.DisplayName}");
    }

    private static void SelectComboBoxByTag(ComboBox comboBox, int tagValue)
    {
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item && item.Tag is int idx && idx == tagValue)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }
    }

    private void RefreshAssignedProperties()
    {
        AssignedPropertiesList.Items.Clear();

        if (_currentItem == null)
            return;

        foreach (var prop in _currentItem.Properties)
        {
            var displayText = ResolvePropertyDisplayText(prop);
            AssignedPropertiesList.Items.Add(new ListBoxItem
            {
                Content = displayText,
                Tag = prop
            });
        }

        RemovePropertyButton.IsEnabled = false;
        EditPropertyButton.IsEnabled = false;
        ClearAllPropertiesButton.IsEnabled = _currentItem.Properties.Count > 0;

        RefreshStatistics();
    }

    private string ResolvePropertyDisplayText(ItemProperty prop)
    {
        if (_itemPropertyService == null)
            return $"Property {prop.PropertyName} (Sub:{prop.Subtype} Cost:{prop.CostValue})";

        // Build display from service data
        var types = _itemPropertyService.GetAvailablePropertyTypes();
        var type = types.FirstOrDefault(t => t.PropertyIndex == prop.PropertyName);
        var name = type?.DisplayName ?? $"Property {prop.PropertyName}";

        var parts = new List<string> { name };

        // Resolve subtype
        if (type?.HasSubtypes == true)
        {
            var subtypes = _itemPropertyService.GetSubtypes(prop.PropertyName);
            var subtype = subtypes.FirstOrDefault(s => s.Index == prop.Subtype);
            if (subtype != null)
                parts.Add(subtype.DisplayName);
        }

        // Resolve cost value
        if (type?.HasCostTable == true)
        {
            var costValues = _itemPropertyService.GetCostValues(prop.PropertyName);
            var cost = costValues.FirstOrDefault(c => c.Index == prop.CostValue);
            if (cost != null)
                parts.Add(cost.DisplayName);
        }

        // Resolve param value
        if (type?.HasParamTable == true && prop.Param1 != 0xFF)
        {
            var paramValues = _itemPropertyService.GetParamValues(prop.PropertyName);
            var param = paramValues.FirstOrDefault(p => p.Index == prop.Param1Value);
            if (param != null)
                parts.Add(param.DisplayName);
        }

        return string.Join(" ", parts);
    }

    private void RefreshStatistics()
    {
        if (_currentItem == null || _itemStatisticsService == null)
        {
            ItemStatisticsPanel.IsVisible = false;
            return;
        }

        var stats = _itemStatisticsService.GenerateStatistics(_currentItem.Properties);
        ItemStatisticsText.Text = stats;
        ItemStatisticsPanel.IsVisible = true;
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
