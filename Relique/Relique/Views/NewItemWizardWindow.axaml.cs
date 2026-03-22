using Avalonia;
using Avalonia.Controls;

using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ItemEditor.Services;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ItemEditor.Views;

public partial class NewItemWizardWindow : Window
{
    private readonly IGameDataService? _gameDataService;
    private readonly List<BaseItemTypeInfo> _allBaseItemTypes;
    private readonly BaseItemCategoryService _categoryService = new();
    private readonly List<PaletteCategory> _paletteCategories;

    private int _currentStep = 1;
    private BaseItemTypeInfo? _selectedType;
    private readonly Dictionary<ItemCategory, CheckBox> _categoryCheckBoxes = new();
    private CancellationTokenSource? _searchDebounce;
    private ItemIconService? _iconService;
    private byte _selectedModelPart1 = 1;

    /// <summary>
    /// Path to the created .uti file, or null if cancelled.
    /// </summary>
    public string? CreatedFilePath { get; private set; }

    /// <summary>
    /// Whether the user wants to open the file in the editor after creation.
    /// </summary>
    public bool OpenInEditor { get; private set; }

    // Parameterless constructor required by Avalonia XAML resource loader
    public NewItemWizardWindow() : this(null, null) { }

    public NewItemWizardWindow(IGameDataService? gameDataService, List<BaseItemTypeInfo>? baseItemTypes)
    {
        InitializeComponent();

        _gameDataService = gameDataService;
        _allBaseItemTypes = baseItemTypes ?? new List<BaseItemTypeInfo>();
        _paletteCategories = LoadPaletteCategories();

        InitializeCategoryCheckboxes();
        PopulatePaletteCategories();
        InitializeDefaults();
        FilterTypeList();

        // Wire up text change events
        TypeSearchBox.TextChanged += OnTypeSearchTextChanged;
        NameTextBox.TextChanged += OnNameTextChanged;
        TagTextBox.TextChanged += OnTagTextChanged;
        ResRefTextBox.TextChanged += OnResRefTextChanged;
    }

    // --- Initialization ---

    private void InitializeCategoryCheckboxes()
    {
        var categories = _categoryService.GetAllCategories();
        foreach (var category in categories)
        {
            var cb = new CheckBox
            {
                Content = _categoryService.GetCategoryDisplayName(category),
                IsChecked = true,
                Tag = category
            };
            cb.Click += OnCategoryCheckboxClick;
            _categoryCheckBoxes[category] = cb;
            CategoryCheckboxPanel.Children.Add(cb);
        }
    }

    private void PopulatePaletteCategories()
    {
        PaletteCategoryComboBox.Items.Clear();
        foreach (var cat in _paletteCategories)
        {
            PaletteCategoryComboBox.Items.Add(new ComboBoxItem
            {
                Content = cat.Name,
                Tag = cat.Id
            });
        }
        if (PaletteCategoryComboBox.Items.Count > 0)
            PaletteCategoryComboBox.SelectedIndex = 0;
    }

    private List<PaletteCategory> LoadPaletteCategories()
    {
        if (_gameDataService != null && _gameDataService.IsConfigured)
        {
            try
            {
                var cats = _gameDataService.GetPaletteCategories(Radoub.Formats.Common.ResourceTypes.Uti).ToList();
                if (cats.Count > 0) return cats;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load palette categories: {ex.Message}");
            }
        }

        // Hardcoded fallback
        return new List<PaletteCategory>
        {
            new() { Id = 0, Name = "Standard" },
            new() { Id = 1, Name = "Custom 1" },
            new() { Id = 2, Name = "Custom 2" },
            new() { Id = 3, Name = "Custom 3" },
        };
    }

    private void InitializeDefaults()
    {
        // Save location default: module working directory (unpacked module folder)
        // NWN modules live at: ~/Documents/Neverwinter Nights/modules/{moduleName}/
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (RadoubSettings.IsValidModulePath(modulePath))
        {
            // If it's a .mod file, use the unpacked directory alongside it
            if (modulePath!.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            {
                var unpackedDir = Path.ChangeExtension(modulePath, null);
                SaveLocationTextBox.Text = Directory.Exists(unpackedDir) ? unpackedDir : Path.GetDirectoryName(modulePath) ?? modulePath;
            }
            else
            {
                SaveLocationTextBox.Text = modulePath;
            }
        }
        else
        {
            // Fallback: NWN modules directory (not root — user must pick a module folder)
            var nwnModules = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Neverwinter Nights", "modules");
            if (Directory.Exists(nwnModules))
                SaveLocationTextBox.Text = nwnModules;
            else
                SaveLocationTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        // Open after create from settings
        OpenAfterCreateCheckBox.IsChecked = SettingsService.Instance.OpenInEditorAfterCreate;
    }

    // --- Step Navigation ---

    private void ShowStep(int step)
    {
        _currentStep = step;
        Step1Panel.IsVisible = step == 1;
        Step2Panel.IsVisible = step == 2;
        Step3Panel.IsVisible = step == 3;

        BackButton.IsEnabled = step > 1;
        ValidationText.Text = string.Empty;

        switch (step)
        {
            case 1:
                Title = "New Item — Step 1 of 3: Item Type";
                NextButton.Content = "Next →";
                UpdateNextEnabled();
                break;
            case 2:
                Title = "New Item — Step 2 of 3: Name & Identity";
                NextButton.Content = "Next →";
                SelectedTypeLabel.Text = _selectedType?.DisplayName ?? "(none)";
                UpdateStep2Validation();
                break;
            case 3:
                Title = "New Item — Step 3 of 3: Save & Finish";
                NextButton.Content = "Create Item";
                PopulateSummary();
                NextButton.IsEnabled = true;
                break;
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
            ShowStep(_currentStep - 1);
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep < 3)
        {
            ShowStep(_currentStep + 1);
        }
        else
        {
            CreateItem();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CreatedFilePath = null;
        Close();
    }

    // --- Step 1: Type Selection ---

    private void FilterTypeList()
    {
        var searchText = TypeSearchBox.Text?.Trim() ?? string.Empty;
        var enabledCategories = _categoryCheckBoxes
            .Where(kvp => kvp.Value.IsChecked == true)
            .Select(kvp => kvp.Key)
            .ToHashSet();

        var filtered = _allBaseItemTypes.Where(t =>
        {
            // Category filter
            if (_gameDataService != null && _gameDataService.IsConfigured)
            {
                var category = _categoryService.CategorizeBaseItem(t.BaseItemIndex, _gameDataService);
                if (!enabledCategories.Contains(category))
                    return false;
            }

            // Text search filter
            if (!string.IsNullOrEmpty(searchText))
            {
                if (!t.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                    !t.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }).ToList();

        TypeListBox.Items.Clear();
        foreach (var type in filtered)
        {
            var prefix = _categoryService.IsCustomContent(type.BaseItemIndex) ? "* " : "";
            TypeListBox.Items.Add(new ListBoxItem
            {
                Content = $"{prefix}{type.DisplayName}",
                Tag = type
            });
        }

        TypeCountText.Text = $"Showing {filtered.Count} of {_allBaseItemTypes.Count} types";
        UpdateNextEnabled();

        PopulateIconVariations();
    }

    private void OnTypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TypeListBox.SelectedItem is ListBoxItem item && item.Tag is BaseItemTypeInfo type)
        {
            _selectedType = type;
            _selectedModelPart1 = 1; // Reset to default when type changes
        }
        else
        {
            _selectedType = null;
        }
        UpdateNextEnabled();
        PopulateIconVariations();
    }

    private void OnTypeSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        // Debounce 300ms
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var token = _searchDebounce.Token;

        DispatcherTimer.RunOnce(() =>
        {
            if (!token.IsCancellationRequested)
                FilterTypeList();
        }, TimeSpan.FromMilliseconds(300));
    }

    private void OnSelectAllCategoriesClick(object? sender, RoutedEventArgs e)
    {
        var isChecked = SelectAllCheckBox.IsChecked == true;
        foreach (var cb in _categoryCheckBoxes.Values)
        {
            cb.IsChecked = isChecked;
        }
        FilterTypeList();
    }

    private void OnCategoryCheckboxClick(object? sender, RoutedEventArgs e)
    {
        // Update "Select All" state
        var allChecked = _categoryCheckBoxes.Values.All(cb => cb.IsChecked == true);
        var noneChecked = _categoryCheckBoxes.Values.All(cb => cb.IsChecked != true);
        SelectAllCheckBox.IsChecked = allChecked ? true : noneChecked ? false : null;
        FilterTypeList();
    }

    private void InitializeIconService()
    {
        if (_iconService != null) return;
        if (_gameDataService != null && _gameDataService.IsConfigured)
        {
            _iconService = new ItemIconService(_gameDataService);
        }
    }

    private void PopulateIconVariations()
    {
        IconGridPanel.Children.Clear();

        if (_selectedType == null || _gameDataService == null || !_gameDataService.IsConfigured)
        {
            IconPanelTitle.Text = "Select an item type to browse icons";
            return;
        }

        InitializeIconService();
        if (_iconService == null) return;

        var baseIdx = _selectedType.BaseItemIndex;
        var minStr = _gameDataService.Get2DAValue("baseitems", baseIdx, "MinRange");
        var maxStr = _gameDataService.Get2DAValue("baseitems", baseIdx, "MaxRange");

        int minRange = 0, maxRange = 0;
        if (int.TryParse(minStr, out int mn)) minRange = mn;
        if (int.TryParse(maxStr, out int mx)) maxRange = mx;

        // Cap to avoid UI lag
        if (maxRange - minRange > 300) maxRange = minRange + 300;

        int iconCount = 0;
        int start = minRange == 0 ? 1 : minRange;

        for (int modelNum = start; modelNum <= maxRange; modelNum++)
        {
            var icon = _iconService.GetItemIcon(baseIdx, modelNum);
            if (icon == null) continue;

            iconCount++;
            AddIconButton(icon, (byte)modelNum, $"{_selectedType.DisplayName} #{modelNum}");
        }

        if (iconCount == 0)
        {
            // No numbered icons — show the default icon (fixed icon type)
            var defaultIcon = _iconService.GetItemIcon(baseIdx);
            if (defaultIcon != null)
            {
                AddIconButton(defaultIcon, 1, $"{_selectedType.DisplayName}");
                iconCount = 1;
            }
        }

        IconPanelTitle.Text = iconCount > 1
            ? $"{_selectedType.DisplayName} — {iconCount} icons"
            : iconCount == 1
                ? $"{_selectedType.DisplayName} — fixed icon (model varies)"
                : $"{_selectedType.DisplayName} — no icons found";
    }

    private void AddIconButton(Bitmap icon, byte modelPart, string tooltip)
    {
        var button = new Button
        {
            Width = 48,
            Height = 48,
            Margin = new Thickness(2),
            Padding = new Thickness(2),
            Tag = modelPart,
        };
        ToolTip.SetTip(button, tooltip);

        button.Content = new Image
        {
            Source = icon,
            Width = 40,
            Height = 40,
            Stretch = Stretch.Uniform
        };

        if (_selectedModelPart1 == modelPart)
        {
            button.BorderBrush = new SolidColorBrush(Colors.DodgerBlue);
            button.BorderThickness = new Thickness(2);
        }

        button.Click += (_, _) =>
        {
            _selectedModelPart1 = modelPart;
            PopulateIconVariations();
        };

        IconGridPanel.Children.Add(button);
    }

    private void UpdateNextEnabled()
    {
        if (_currentStep == 1)
        {
            NextButton.IsEnabled = _selectedType != null;
        }
    }

    // --- Step 2: Name & Identity ---

    private void OnNameTextChanged(object? sender, TextChangedEventArgs e)
    {
        var name = NameTextBox.Text ?? string.Empty;

        if (AutoTagCheckBox.IsChecked == true)
        {
            TagTextBox.Text = ItemNamingService.GenerateTag(name);
        }
        if (AutoResRefCheckBox.IsChecked == true)
        {
            ResRefTextBox.Text = ItemNamingService.GenerateResRef(name);
        }

        UpdateStep2Validation();
    }

    private void OnTagTextChanged(object? sender, TextChangedEventArgs e)
    {
        var tag = TagTextBox.Text ?? string.Empty;
        TagCharCount.Text = $"{tag.Length}/32";
        UpdateStep2Validation();
    }

    private void OnResRefTextChanged(object? sender, TextChangedEventArgs e)
    {
        var resRef = ResRefTextBox.Text ?? string.Empty;
        ResRefCharCount.Text = $"{resRef.Length}/16";
        UpdateFilenameLabel();
        UpdateStep2Validation();
    }

    private void OnAutoTagCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (AutoTagCheckBox.IsChecked == true)
        {
            TagTextBox.Text = ItemNamingService.GenerateTag(NameTextBox.Text ?? string.Empty);
            TagTextBox.IsReadOnly = true;
        }
        else
        {
            TagTextBox.IsReadOnly = false;
        }
    }

    private void OnAutoResRefCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (AutoResRefCheckBox.IsChecked == true)
        {
            ResRefTextBox.Text = ItemNamingService.GenerateResRef(NameTextBox.Text ?? string.Empty);
            ResRefTextBox.IsReadOnly = true;
        }
        else
        {
            ResRefTextBox.IsReadOnly = false;
        }
    }

    private void UpdateStep2Validation()
    {
        var name = NameTextBox.Text?.Trim() ?? string.Empty;
        var tag = TagTextBox.Text ?? string.Empty;
        var resRef = ResRefTextBox.Text ?? string.Empty;

        string? error = null;

        if (string.IsNullOrEmpty(name))
            error = "Name is required.";
        else if (!string.IsNullOrEmpty(tag) && !ItemNamingService.IsValidTag(tag))
            error = "Tag contains invalid characters (use A-Z, 0-9, underscore).";
        else if (string.IsNullOrEmpty(resRef))
            error = "ResRef is required.";
        else if (!ItemNamingService.IsValidResRef(resRef))
            error = "ResRef must be 1-16 lowercase alphanumeric/underscore characters.";

        ValidationText.Text = error ?? string.Empty;
        NextButton.IsEnabled = error == null && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(resRef);
    }

    private void UpdateFilenameLabel()
    {
        var resRef = ResRefTextBox.Text ?? string.Empty;
        FilenameLabel.Text = string.IsNullOrEmpty(resRef) ? "(enter a name)" : $"{resRef}.uti";
    }

    // --- Step 3: Save & Finish ---

    private void PopulateSummary()
    {
        SummaryType.Text = _selectedType?.DisplayName ?? "(none)";
        SummaryName.Text = NameTextBox.Text ?? string.Empty;
        SummaryTag.Text = TagTextBox.Text ?? string.Empty;
        SummaryResRef.Text = ResRefTextBox.Text ?? string.Empty;
        UpdateFilenameLabel();
    }

    private async void OnBrowseSaveLocationClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Save Location",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
                SaveLocationTextBox.Text = path;
        }
    }

    // --- Item Creation ---

    private void CreateItem()
    {
        if (_selectedType == null) return;

        var name = NameTextBox.Text?.Trim() ?? string.Empty;
        var tag = TagTextBox.Text ?? string.Empty;
        var resRef = ResRefTextBox.Text ?? string.Empty;
        var saveDir = SaveLocationTextBox.Text ?? string.Empty;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(resRef) || string.IsNullOrEmpty(saveDir))
        {
            ValidationText.Text = "Please fill in all required fields.";
            return;
        }

        if (!Directory.Exists(saveDir))
        {
            try
            {
                Directory.CreateDirectory(saveDir);
            }
            catch (Exception ex)
            {
                ValidationText.Text = $"Cannot create directory: {ex.Message}";
                return;
            }
        }

        try
        {
            // Resolve ResRef conflicts
            var finalResRef = ItemNamingService.ResolveResRefConflict(
                resRef, r => File.Exists(Path.Combine(saveDir, r + ".uti")));

            // Get palette category
            byte paletteId = 0;
            if (PaletteCategoryComboBox.SelectedItem is ComboBoxItem catItem && catItem.Tag is byte catId)
                paletteId = catId;

            // Create UTI file
            var uti = new UtiFile
            {
                TemplateResRef = finalResRef,
                Tag = tag,
                BaseItem = _selectedType.BaseItemIndex,
                Identified = true,
                Dropable = true,
                ModelPart1 = _selectedModelPart1,
                PaletteID = paletteId,
            };
            uti.LocalizedName.SetString(0, name);

            // Apply defaults from baseitems.2da if available
            if (_gameDataService != null && _gameDataService.IsConfigured)
            {
                var baseCost = _gameDataService.Get2DAValue("baseitems", _selectedType.BaseItemIndex, "BaseCost");
                if (baseCost != null && baseCost != "****" && uint.TryParse(baseCost, out var cost))
                    uti.Cost = cost;
            }

            var filePath = Path.Combine(saveDir, finalResRef + ".uti");
            UtiWriter.Write(uti, filePath);

            // Save settings
            OpenInEditor = OpenAfterCreateCheckBox.IsChecked == true;
            SettingsService.Instance.OpenInEditorAfterCreate = OpenInEditor;

            CreatedFilePath = filePath;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Created new item: {UnifiedLogger.SanitizePath(filePath)}");

            Close();
        }
        catch (Exception ex)
        {
            ValidationText.Text = $"Error creating item: {ex.Message}";
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to create item: {ex.Message}");
        }
    }
}
