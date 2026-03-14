using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Quartermaster.Views.Dialogs;
using Quartermaster.Views.Helpers;
using Quartermaster.Views.Panels;
using Radoub.Formats.Bic;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Utc;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using DialogHelper = Quartermaster.Views.Helpers.DialogHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Views;

/// <summary>
/// Main window for Quartermaster creature/inventory editor.
/// Partial class files: FileOps, Inventory, ItemPalette, ItemResolution,
/// CreatureBrowser, Lifecycle, MenuDialogs.
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private UtcFile? _currentCreature;
    private readonly Radoub.UI.Services.DocumentState _documentState = new("Quartermaster");
    private bool _isBicFile;
    private string _currentSection = "Character";
    private Button? _selectedNavButton;

    // Services initialized in InitializeServicesAsync, awaited before any use.
    // Access via GameData/DisplayService/etc. properties which throw if not yet initialized.
    private IGameDataService? _gameDataService;
    private CreatureDisplayService? _creatureDisplayService;
    private ItemViewModelFactory? _itemViewModelFactory;
    private ItemIconService? _itemIconService;
    private AudioService? _audioService;
    private bool _servicesInitialized;

    // Service accessors with null guards - throw clear error instead of NullReferenceException
    private IGameDataService GameData => _gameDataService ?? throw new InvalidOperationException("GameDataService not initialized - services must be initialized before use");
    private CreatureDisplayService DisplayService => _creatureDisplayService ?? throw new InvalidOperationException("CreatureDisplayService not initialized");
    private ItemViewModelFactory ItemFactory => _itemViewModelFactory ?? throw new InvalidOperationException("ItemViewModelFactory not initialized");
    private ItemIconService IconService => _itemIconService ?? throw new InvalidOperationException("ItemIconService not initialized");
    private AudioService Audio => _audioService ?? throw new InvalidOperationException("AudioService not initialized");

    // Cancellation token for async operations - cancelled on window close
    private CancellationTokenSource? _windowCts;

    // Equipment slots collection (shared with InventoryPanel)
    private ObservableCollection<EquipmentSlotViewModel> _equipmentSlots = new();

    // Convenience accessors for document state (used across partial files)
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

    // Selection state tracking
    private bool _hasSelection;

    // Bindable properties for UI state
    public bool HasFile => _currentCreature != null;
    public bool HasSelection
    {
        get => _hasSelection;
        private set { _hasSelection = value; OnPropertyChanged(); }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();

        // Wire up shared document state for title bar updates
        _documentState.DirtyStateChanged += () => Title = _documentState.GetTitle(_isBicFile ? " (Player)" : null);

        // Only do fast UI setup in constructor - defer heavy I/O to OnWindowOpened
        InitializeEquipmentSlots();
        RestoreWindowPosition();

        // Set initial nav button selection
        _selectedNavButton = NavCharacter;

        // Initialize creature browser panel (#1145)
        InitializeCreatureBrowserPanel();

        // Show module context in status bar (#1003)
        UpdateModuleIndicator();

        Closing += OnWindowClosing;
        Opened += OnWindowOpened;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Quartermaster MainWindow initialized");
    }

    private void InitializeEquipmentSlots()
    {
        // Create all equipment slots using the factory
        var allSlots = EquipmentSlotFactory.CreateAllSlots();
        foreach (var slot in allSlots)
        {
            _equipmentSlots.Add(slot);
        }

        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Initialized {_equipmentSlots.Count} equipment slots");
    }

    private void InitializePanels()
    {
        // Initialize stats panel with display service
        StatsPanelContent.SetDisplayService(DisplayService);
        StatsPanelContent.CRAdjustChanged += (s, e) => MarkDirty();
        StatsPanelContent.AbilityScoresChanged += (s, e) => MarkDirty();
        StatsPanelContent.HitPointsChanged += (s, e) => MarkDirty();
        StatsPanelContent.NaturalAcChanged += (s, e) => MarkDirty();
        StatsPanelContent.SavingThrowsChanged += (s, e) => MarkDirty();

        // Initialize character panel with display service
        CharacterPanelContent.SetDisplayService(DisplayService);
        CharacterPanelContent.SetGameDataService(GameData);
        CharacterPanelContent.SetItemIconService(IconService);
        CharacterPanelContent.SetAudioService(Audio);
        CharacterPanelContent.CharacterChanged += (s, e) => MarkDirty();
        CharacterPanelContent.PortraitChanged += (s, e) => UpdateCharacterHeader();

        // Initialize classes panel with display service for 2DA/TLK lookups
        ClassesPanelContent.SetDisplayService(DisplayService);
        ClassesPanelContent.AlignmentChanged += (s, e) => MarkDirty();
        ClassesPanelContent.PackageChanged += (s, e) => MarkDirty();
        ClassesPanelContent.ClassesChanged += OnClassesChanged;
        ClassesPanelContent.LevelUpRequested += (s, e) => LaunchLevelUpWizard();

        // Initialize feats panel with display service for 2DA/TLK lookups
        FeatsPanelContent.SetDisplayService(DisplayService);
        FeatsPanelContent.SetIconService(IconService);
        FeatsPanelContent.FeatsChanged += (s, e) => MarkDirty();

        // Initialize special abilities panel with display service
        SpecialAbilitiesPanelContent.SetDisplayService(DisplayService);
        SpecialAbilitiesPanelContent.SpecialAbilitiesChanged += (s, e) => MarkDirty();

        // Initialize skills panel with display service for 2DA/TLK lookups
        SkillsPanelContent.SetDisplayService(DisplayService);
        SkillsPanelContent.SetIconService(IconService);
        SkillsPanelContent.SkillsChanged += (s, e) => MarkDirty();

        // Initialize spells panel with display service for 2DA/TLK lookups
        SpellsPanelContent.SetDisplayService(DisplayService);
        SpellsPanelContent.SetIconService(IconService);
        SpellsPanelContent.SpellsChanged += (s, e) => MarkDirty();

        // Initialize appearance panel with display service, palette colors, model service, and texture service
        AppearancePanelContent.SetDisplayService(DisplayService);
        AppearancePanelContent.SetPaletteColorService(new PaletteColorService(GameData));
        AppearancePanelContent.SetModelService(new ModelService(GameData));
        AppearancePanelContent.SetTextureService(new TextureService(GameData));
        AppearancePanelContent.AppearanceChanged += (s, e) => MarkDirty();

        // Initialize advanced panel with display service
        AdvancedPanelContent.SetDisplayService(DisplayService);
        AdvancedPanelContent.TagChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.CommentChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.FlagsChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.BehaviorChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.PaletteCategoryChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.VariablesChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.RenameRequested += OnRenameRequested;

        // Initialize scripts panel with game data service
        ScriptsPanelContent.SetGameDataService(GameData);
        ScriptsPanelContent.ScriptsChanged += (s, e) => MarkDirty();

        // Initialize inventory panel with shared equipment slots and game data
        InventoryPanelContent.InitializeSlots(_equipmentSlots);
        InventoryPanelContent.SetGameDataService(GameData);

        // Provide item resolver for cache-loaded palette items needing full details
        InventoryPanelContent.ItemResolver = ResolveItemForDetails;

        // Subscribe to inventory panel events
        InventoryPanelContent.InventoryChanged += (s, e) => { _inventoryModified = true; MarkDirty(); };
        InventoryPanelContent.EquipmentSlotClicked += (s, slot) =>
        {
            HasSelection = slot.HasItem;
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"Equipment slot clicked: {slot.Name}");
        };
        InventoryPanelContent.EquipmentSlotDoubleClicked += (s, slot) =>
        {
            if (slot.HasItem)
            {
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Equipment slot double-clicked: {slot.Name} - unequipping {slot.EquippedItem?.Name}");
                UnequipToBackpack(slot);
            }
        };
        InventoryPanelContent.EquipmentSlotItemDropped += OnEquipmentSlotItemDropped;
        InventoryPanelContent.BackpackItemDropped += OnBackpackItemDropped;
        InventoryPanelContent.AddToBackpackRequested += OnAddToBackpackRequested;
        InventoryPanelContent.EquipItemsRequested += OnEquipItemsRequested;
        InventoryPanelContent.UnequipToBackpackRequested += (s, slot) => UnequipToBackpack(slot);
        InventoryPanelContent.EquipFromBackpackRequested += OnEquipFromBackpackRequested;
        InventoryPanelContent.DeleteFromBackpackRequested += OnDeleteFromBackpackRequested;
    }

    #region Navigation

    private void OnNavButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button clickedButton) return;

        var section = clickedButton.Tag?.ToString();
        if (string.IsNullOrEmpty(section)) return;

        NavigateToSection(section, clickedButton);
    }

    private void NavigateToSection(string section, Button? navButton = null)
    {
        if (_currentSection == section) return;

        // Update nav button selection
        if (_selectedNavButton != null)
        {
            _selectedNavButton.Classes.Remove("Selected");
        }

        navButton ??= section switch
        {
            "Stats" => NavStats,
            "Character" => NavCharacter,
            "Classes" => NavClasses,
            "Skills" => NavSkills,
            "Feats" => NavFeats,
            "SpecialAbilities" => NavSpecialAbilities,
            "Spells" => NavSpells,
            "Inventory" => NavInventory,
            "Appearance" => NavAppearance,
            "Advanced" => NavAdvanced,
            "Scripts" => NavScripts,
            "QuickBar" => NavQuickBar,
            _ => null
        };

        if (navButton != null)
        {
            navButton.Classes.Add("Selected");
            _selectedNavButton = navButton;
        }

        // Hide all panels
        StatsPanelContent.IsVisible = false;
        CharacterPanelContent.IsVisible = false;
        ClassesPanelContent.IsVisible = false;
        SkillsPanelContent.IsVisible = false;
        FeatsPanelContent.IsVisible = false;
        SpecialAbilitiesPanelContent.IsVisible = false;
        SpellsPanelContent.IsVisible = false;
        InventoryPanelContent.IsVisible = false;
        AppearancePanelContent.IsVisible = false;
        AdvancedPanelContent.IsVisible = false;
        ScriptsPanelContent.IsVisible = false;
        QuickBarPanelContent.IsVisible = false;

        // Show selected panel
        switch (section)
        {
            case "Stats":
                StatsPanelContent.IsVisible = true;
                break;
            case "Character":
                CharacterPanelContent.IsVisible = true;
                break;
            case "Classes":
                ClassesPanelContent.IsVisible = true;
                break;
            case "Skills":
                SkillsPanelContent.IsVisible = true;
                break;
            case "Feats":
                FeatsPanelContent.IsVisible = true;
                break;
            case "SpecialAbilities":
                SpecialAbilitiesPanelContent.IsVisible = true;
                break;
            case "Spells":
                SpellsPanelContent.IsVisible = true;
                break;
            case "Inventory":
                InventoryPanelContent.IsVisible = true;
                // Load palette items on-demand when first navigating to Inventory
                if (_windowCts != null)
                    _ = LoadPaletteItemsAsync(_windowCts.Token);
                break;
            case "Appearance":
                AppearancePanelContent.IsVisible = true;
                break;
            case "Advanced":
                AdvancedPanelContent.IsVisible = true;
                break;
            case "Scripts":
                ScriptsPanelContent.IsVisible = true;
                break;
            case "QuickBar":
                QuickBarPanelContent.IsVisible = true;
                break;
        }

        _currentSection = section;
        UnifiedLogger.LogUI(LogLevel.DEBUG, $"Navigated to section: {section}");
    }

    #endregion

    #region Edit Operations

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_currentSection == "Inventory")
        {
            InventoryPanelContent.DeleteSelectedBackpackItems();
            _inventoryModified = true;
            MarkDirty();
        }
    }

    private void OnClassesChanged(object? sender, EventArgs e)
    {
        // Mark document as dirty
        MarkDirty();

        // Refresh dependent panels (stats recalc, feat list for domain feat swaps, domain spells)
        if (_currentCreature != null)
        {
            StatsPanelContent.LoadCreature(_currentCreature);
            FeatsPanelContent.LoadCreature(_currentCreature);
            SpecialAbilitiesPanelContent.LoadCreature(_currentCreature);
            SpellsPanelContent.LoadCreature(_currentCreature);
            UpdateCharacterHeader();
        }
    }

    #endregion

    #region UI Updates

    private void UpdateTitle()
    {
        Title = _documentState.GetTitle(_isBicFile ? " (Player)" : null);
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void UpdateCharacterHeader()
    {
        try
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "UpdateCharacterHeader: Starting");
            var portraitImage = this.FindControl<Avalonia.Controls.Image>("PortraitImage");
            var portraitPlaceholder = this.FindControl<TextBlock>("PortraitPlaceholderText");

            if (_currentCreature == null)
            {
                CharacterNameText.Text = "No Character Loaded";
                CharacterSummaryText.Text = "";
                if (portraitImage != null)
                {
                    portraitImage.Source = null;
                    portraitImage.IsVisible = false;
                }
                if (portraitPlaceholder != null)
                    portraitPlaceholder.IsVisible = true;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "UpdateCharacterHeader: No creature, done");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "UpdateCharacterHeader: Setting name");
            CharacterNameText.Text = CreatureDisplayService.GetCreatureFullName(_currentCreature);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "UpdateCharacterHeader: Setting summary");
            CharacterSummaryText.Text = DisplayService.GetCreatureSummary(_currentCreature);

            if (portraitImage != null && portraitPlaceholder != null)
            {
                string? portraitResRef;
                if (_currentCreature.PortraitId > 0)
                {
                    portraitResRef = DisplayService.GetPortraitResRef(_currentCreature.PortraitId);
                }
                else if (!string.IsNullOrEmpty(_currentCreature.Portrait))
                {
                    portraitResRef = _currentCreature.Portrait;
                }
                else
                {
                    portraitResRef = null;
                }
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Portrait lookup: PortraitId={_currentCreature.PortraitId}, Portrait='{_currentCreature.Portrait ?? ""}', ResRef={portraitResRef ?? "null"}");
                if (!string.IsNullOrEmpty(portraitResRef))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UpdateCharacterHeader: Loading portrait {portraitResRef}");
                    var portrait = IconService.GetPortrait(portraitResRef);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UpdateCharacterHeader: GetPortrait returned {(portrait != null ? "bitmap" : "null")}");
                    if (portrait != null)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, "UpdateCharacterHeader: Setting portrait source");
                        portraitImage.Source = portrait;
                        portraitImage.IsVisible = true;
                        portraitPlaceholder.IsVisible = false;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Portrait loaded: {portraitResRef}");
                        return;
                    }
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Portrait not found: {portraitResRef}");
                }
                portraitImage.Source = null;
                portraitImage.IsVisible = false;
                portraitPlaceholder.IsVisible = true;
            }
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "UpdateCharacterHeader: Complete");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"UpdateCharacterHeader crashed: {ex}");
        }
    }

    private void UpdateInventoryCounts()
    {
        if (_currentCreature == null)
        {
            InventoryCountText.Text = "";
            FilePathText.Text = "";
            return;
        }

        InventoryPanelContent.UpdateInventoryCounts(out var equippedCount, out var backpackCount);
        InventoryCountText.Text = $"{equippedCount} equipped, {backpackCount} in backpack";
        FilePathText.Text = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "";
    }

    private void MarkDirty([CallerMemberName] string? caller = null)
    {
        _documentState.MarkDirty(caller);
    }

    private void LoadAllPanels(UtcFile? creature)
    {
        StatsPanelContent.SetFileType(_isBicFile);
        StatsPanelContent.LoadCreature(creature);

        var equippedItems = _equipmentSlots
            .Where(s => s.HasItem && s.EquippedItem?.Item != null)
            .Select(s => s.EquippedItem!.Item);
        StatsPanelContent.SetEquippedItems(equippedItems);

        CharacterPanelContent.SetFileType(_isBicFile);
        CharacterPanelContent.LoadCreature(creature);
        ClassesPanelContent.LoadCreature(creature);
        SkillsPanelContent.LoadCreature(creature);
        FeatsPanelContent.LoadCreature(creature);
        SpecialAbilitiesPanelContent.LoadCreature(creature);
        SpellsPanelContent.LoadCreature(creature);
        ScriptsPanelContent.LoadCreature(creature);
        AppearancePanelContent.LoadCreature(creature);
        AdvancedPanelContent.SetFileType(_isBicFile);
        AdvancedPanelContent.LoadCreature(creature);

        if (_isBicFile && creature is BicFile bicFile)
        {
            QuickBarPanelContent.LoadQuickBar(bicFile);
        }
        else
        {
            QuickBarPanelContent.ClearPanel();
        }

        UpdateFileTypeVisibility();
    }

    private void UpdateFileTypeVisibility()
    {
        NavScripts.IsVisible = !_isBicFile;
        NavQuickBar.IsVisible = _isBicFile;

        if (_isBicFile && _currentSection == "Scripts")
        {
            NavigateToSection("Character", NavCharacter);
        }

        if (!_isBicFile && _currentSection == "QuickBar")
        {
            NavigateToSection("Character", NavCharacter);
        }
    }

    private void ClearAllPanels()
    {
        StatsPanelContent.SetFileType(false);
        StatsPanelContent.ClearStats();
        CharacterPanelContent.SetFileType(false);
        CharacterPanelContent.ClearPanel();
        ClassesPanelContent.ClearPanel();
        SkillsPanelContent.ClearPanel();
        FeatsPanelContent.ClearPanel();
        SpecialAbilitiesPanelContent.ClearPanel();
        SpellsPanelContent.ClearPanel();
        ScriptsPanelContent.ClearPanel();
        AppearancePanelContent.ClearPanel();
        AdvancedPanelContent.SetFileType(false);
        AdvancedPanelContent.ClearPanel();
        QuickBarPanelContent.ClearPanel();

        NavScripts.IsVisible = true;
        NavQuickBar.IsVisible = false;
    }

    #endregion

    #region Keyboard Shortcuts

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    _ = NewFile();
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
                case Key.E:
                    if (HasFile)
                    {
                        _ = ExportCharacterSheet(isMarkdown: false);
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
                case Key.F4:
                    OnToggleCreatureBrowserClick(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
    }

    #endregion

    #region Language Menu (#1363)

    private void PopulateLanguageMenu()
    {
        var languageMenu = this.FindControl<MenuItem>("LanguageMenu");
        if (languageMenu == null) return;

        languageMenu.Items.Clear();

        var settings = RadoubSettings.Instance;
        var availableLanguages = settings.GetAvailableTlkLanguages().ToList();

        if (availableLanguages.Count == 0)
        {
            var noLangItem = new MenuItem { Header = "(No languages detected)", IsEnabled = false };
            languageMenu.Items.Add(noLangItem);
            return;
        }

        var currentLang = settings.EffectiveLanguage;
        var currentFemale = settings.TlkUseFemale;

        foreach (var language in availableLanguages)
        {
            var langName = LanguageHelper.GetDisplayName(language);
            var langCode = LanguageHelper.GetLanguageCode(language);

            var maleItem = new MenuItem
            {
                Header = $"{langName}",
                Tag = (langCode, false),
                Icon = (language == currentLang && !currentFemale) ? new TextBlock { Text = "✓" } : null
            };
            maleItem.Click += OnLanguageMenuItemClick;
            languageMenu.Items.Add(maleItem);

            var femaleTlkPath = settings.GetTlkPath(language, Gender.Female);
            var maleTlkPath = settings.GetTlkPath(language, Gender.Male);
            var hasFemaleVariant = femaleTlkPath != null && maleTlkPath != null
                && !string.Equals(femaleTlkPath, maleTlkPath, StringComparison.OrdinalIgnoreCase);

            if (hasFemaleVariant)
            {
                var femaleItem = new MenuItem
                {
                    Header = $"{langName} (Female)",
                    Tag = (langCode, true),
                    Icon = (language == currentLang && currentFemale) ? new TextBlock { Text = "✓" } : null
                };
                femaleItem.Click += OnLanguageMenuItemClick;
                languageMenu.Items.Add(femaleItem);
            }
        }
    }

    private async void OnLanguageMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not (string langCode, bool useFemale))
            return;

        var settings = RadoubSettings.Instance;
        var oldLang = settings.TlkLanguage;
        var oldFemale = settings.TlkUseFemale;

        if (langCode == oldLang && useFemale == oldFemale)
            return;

        settings.TlkLanguage = langCode;
        settings.TlkUseFemale = useFemale;

        var langDisplay = LanguageHelper.GetDisplayName(
            LanguageHelper.FromLanguageCode(langCode) ?? Language.English);
        var genderDisplay = useFemale ? " (Female)" : "";

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Language changed to {langDisplay}{genderDisplay}");

        PopulateLanguageMenu();

        UpdateStatus($"Switching to {langDisplay}{genderDisplay}...");

        try
        {
            await Task.Run(() =>
            {
                _gameDataService?.ReloadConfiguration();

                // Re-configure module HAKs after settings reload (#1314)
                var moduleDir = GetModuleWorkingDirectory();
                if (!string.IsNullOrEmpty(moduleDir))
                {
                    _gameDataService?.ConfigureModuleHaks(moduleDir);
                }
            });

            if (_creatureDisplayService != null)
            {
                await _creatureDisplayService.Feats.RebuildCacheAsync();
            }

            await ClearAndReloadPaletteCacheAsync();

            if (_currentCreature != null)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LoadAllPanels(_currentCreature);
                    UpdateCharacterHeader();
                });
            }

            UpdateStatus($"Language: {langDisplay}{genderDisplay} - Ready");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Language switch failed: {ex.Message}");
            UpdateStatus("Language switch failed");
        }
    }

    #endregion

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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
