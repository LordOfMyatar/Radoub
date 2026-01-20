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
using Radoub.Formats.Utc;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Quartermaster.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private UtcFile? _currentCreature;
    private string? _currentFilePath;
    private bool _isDirty;
    private bool _isBicFile;
    private bool _isLoading;
    private string _currentSection = "Character";
    private Button? _selectedNavButton;

    // Game data service for BIF/TLK lookups
    private readonly IGameDataService _gameDataService;
    private readonly CreatureDisplayService _creatureDisplayService;
    private readonly ItemViewModelFactory _itemViewModelFactory;
    private readonly ItemIconService _itemIconService;
    private readonly AudioService _audioService;

    // Equipment slots collection (shared with InventoryPanel)
    private ObservableCollection<EquipmentSlotViewModel> _equipmentSlots = new();

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

        // Initialize game data service for BIF/TLK lookups
        _gameDataService = new GameDataService();
        _creatureDisplayService = new CreatureDisplayService(_gameDataService);
        _itemViewModelFactory = new ItemViewModelFactory(_gameDataService);
        _itemIconService = new ItemIconService(_gameDataService);
        _audioService = new AudioService();

        if (_gameDataService.IsConfigured)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "GameDataService initialized - BIF lookup enabled");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "GameDataService not configured - BIF lookup disabled");
        }

        InitializeEquipmentSlots();
        InitializePanels();
        RestoreWindowPosition();

        // Set initial nav button selection
        _selectedNavButton = NavCharacter;

        Closing += OnWindowClosing;
        Opened += OnWindowOpened;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Quartermaster MainWindow initialized with sidebar layout");
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
        StatsPanelContent.SetDisplayService(_creatureDisplayService);
        StatsPanelContent.CRAdjustChanged += (s, e) => MarkDirty();
        StatsPanelContent.AbilityScoresChanged += (s, e) => MarkDirty();
        StatsPanelContent.HitPointsChanged += (s, e) => MarkDirty();
        StatsPanelContent.NaturalAcChanged += (s, e) => MarkDirty();

        // Initialize character panel with display service
        CharacterPanelContent.SetDisplayService(_creatureDisplayService);
        CharacterPanelContent.SetGameDataService(_gameDataService);
        CharacterPanelContent.SetItemIconService(_itemIconService);
        CharacterPanelContent.SetAudioService(_audioService);
        CharacterPanelContent.CharacterChanged += (s, e) => MarkDirty();
        CharacterPanelContent.PortraitChanged += (s, e) => UpdateCharacterHeader();

        // Initialize classes panel with display service for 2DA/TLK lookups
        ClassesPanelContent.SetDisplayService(_creatureDisplayService);
        ClassesPanelContent.AlignmentChanged += (s, e) => MarkDirty();
        ClassesPanelContent.PackageChanged += (s, e) => MarkDirty();
        ClassesPanelContent.ClassesChanged += OnClassesChanged;

        // Initialize feats panel with display service for 2DA/TLK lookups
        FeatsPanelContent.SetDisplayService(_creatureDisplayService);
        FeatsPanelContent.SetIconService(_itemIconService);

        // Initialize skills panel with display service for 2DA/TLK lookups
        SkillsPanelContent.SetDisplayService(_creatureDisplayService);
        SkillsPanelContent.SetIconService(_itemIconService);
        SkillsPanelContent.SkillsChanged += (s, e) => MarkDirty();

        // Initialize spells panel with display service for 2DA/TLK lookups
        SpellsPanelContent.SetDisplayService(_creatureDisplayService);
        SpellsPanelContent.SetIconService(_itemIconService);
        SpellsPanelContent.SpellsChanged += (s, e) => MarkDirty();

        // Initialize appearance panel with display service, palette colors, model service, and texture service
        AppearancePanelContent.SetDisplayService(_creatureDisplayService);
        AppearancePanelContent.SetPaletteColorService(new PaletteColorService(_gameDataService));
        AppearancePanelContent.SetModelService(new ModelService(_gameDataService));
        AppearancePanelContent.SetTextureService(new TextureService(_gameDataService));
        AppearancePanelContent.AppearanceChanged += (s, e) => MarkDirty();

        // Initialize advanced panel with display service
        AdvancedPanelContent.SetDisplayService(_creatureDisplayService);
        AdvancedPanelContent.TagChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.CommentChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.FlagsChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.BehaviorChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.PaletteCategoryChanged += (s, e) => MarkDirty();

        // Initialize scripts panel with game data service
        ScriptsPanelContent.SetGameDataService(_gameDataService);
        ScriptsPanelContent.ScriptsChanged += (s, e) => MarkDirty();

        // Initialize inventory panel with shared equipment slots and game data
        InventoryPanelContent.InitializeSlots(_equipmentSlots);
        InventoryPanelContent.SetGameDataService(_gameDataService);

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
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"Equipment slot double-clicked: {slot.Name} - {slot.EquippedItem?.Name}");
            }
        };
        InventoryPanelContent.EquipmentSlotItemDropped += (s, e) =>
        {
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"Item dropped on slot: {e.TargetSlot.Name}");
            MarkDirty();
        };
        InventoryPanelContent.BackpackItemDropped += OnBackpackItemDropped;
        InventoryPanelContent.AddToBackpackRequested += OnAddToBackpackRequested;
        InventoryPanelContent.EquipItemsRequested += OnEquipItemsRequested;
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
            case "Spells":
                SpellsPanelContent.IsVisible = true;
                break;
            case "Inventory":
                InventoryPanelContent.IsVisible = true;
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

    #region Window Lifecycle

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;

        UpdateRecentFilesMenu();

        // Initialize caches for better performance
        await InitializeCachesAsync();

        // Start loading game items in background immediately
        StartGameItemsLoad();

        await HandleStartupFileAsync();
    }

    private async Task InitializeCachesAsync()
    {
        if (_gameDataService.IsConfigured)
        {
            UpdateStatus("Loading game data caches...");
            try
            {
                await _creatureDisplayService.InitializeCachesAsync();
                UnifiedLogger.LogApplication(LogLevel.INFO, "Game data caches initialized");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Cache initialization failed: {ex.Message}");
            }
            UpdateStatus("Ready");
        }
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

        // Restore sidebar width
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            MainGrid.ColumnDefinitions[0].Width = new Avalonia.Controls.GridLength(settings.SidebarWidth);
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

        // Save sidebar width
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            settings.SidebarWidth = MainGrid.ColumnDefinitions[0].Width.Value;
        }
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isDirty)
        {
            e.Cancel = true;
            var result = await DialogHelper.ShowUnsavedChangesDialog(this);
            if (result == "Save")
            {
                await SaveFile();
                _isDirty = false; // Clear dirty before Close() to prevent re-entry
                Close();
            }
            else if (result == "Discard")
            {
                _isDirty = false;
                Close();
            }
            // Cancel: do nothing, window stays open
        }
        else
        {
            SaveWindowPosition();
            _audioService.Dispose();
            _gameDataService.Dispose();
        }
    }

    #endregion

    #region Edit Operations

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        // Delegate to inventory panel if on inventory section
        if (_currentSection == "Inventory")
        {
            // The inventory panel handles its own deletion
            UnifiedLogger.LogUI(LogLevel.DEBUG, "Delete clicked on inventory section");
        }
    }

    private void OnClassesChanged(object? sender, EventArgs e)
    {
        // Mark document as dirty
        MarkDirty();

        // Refresh stats panel to show updated saves and character summary
        if (_currentCreature != null)
        {
            StatsPanelContent.LoadCreature(_currentCreature);
            UpdateCharacterHeader();
        }
    }

    #endregion

    #region UI Updates

    private void UpdateTitle()
    {
        var displayPath = _currentFilePath != null ? UnifiedLogger.SanitizePath(_currentFilePath) : "Untitled";
        var dirty = _isDirty ? "*" : "";
        var fileType = _isBicFile ? " (Player)" : "";
        Title = $"Quartermaster - {displayPath}{fileType}{dirty}";
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
                // Clear portrait
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

            // Get character name using display service
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "UpdateCharacterHeader: Setting name");
            CharacterNameText.Text = CreatureDisplayService.GetCreatureFullName(_currentCreature);

            // Build race/class summary using display service
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "UpdateCharacterHeader: Setting summary");
            CharacterSummaryText.Text = _creatureDisplayService.GetCreatureSummary(_currentCreature);

            // Load portrait image (#916)
            if (portraitImage != null && portraitPlaceholder != null)
            {
                // Use PortraitId if set, otherwise use Portrait string field directly
                // BIC files often have PortraitId=0 but a valid Portrait string (e.g., "po_hu_f_07_")
                string? portraitResRef;
                if (_currentCreature.PortraitId > 0)
                {
                    portraitResRef = _creatureDisplayService.GetPortraitResRef(_currentCreature.PortraitId);
                }
                else if (!string.IsNullOrEmpty(_currentCreature.Portrait))
                {
                    // Use the Portrait string field directly - it's already the ResRef
                    portraitResRef = _currentCreature.Portrait;
                }
                else
                {
                    portraitResRef = null;
                }
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Portrait lookup: PortraitId={_currentCreature.PortraitId}, Portrait='{_currentCreature.Portrait ?? ""}', ResRef={portraitResRef ?? "null"}");
                if (!string.IsNullOrEmpty(portraitResRef))
                {
                    // ImageService.GetPortrait handles size suffix internally (tries m, l, s)
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UpdateCharacterHeader: Loading portrait {portraitResRef}");
                    var portrait = _itemIconService.GetPortrait(portraitResRef);
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
                // No portrait available - show placeholder
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
        // Don't mark dirty during file loading - panels may fire change events
        if (_isLoading)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"MarkDirty: Blocked (isLoading=true) from {caller}");
            return;
        }

        // Don't mark dirty if no file is loaded - stale events from clearing panels
        if (_currentCreature == null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"MarkDirty: Blocked (no file loaded) from {caller}");
            return;
        }

        if (!_isDirty)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"MarkDirty: Setting dirty from {caller}");
            _isDirty = true;
            UpdateTitle();
        }
    }

    private void LoadAllPanels(UtcFile? creature)
    {
        // Pass file type to panels that need it for BIC-specific handling
        StatsPanelContent.SetFileType(_isBicFile);
        StatsPanelContent.LoadCreature(creature);

        // Pass equipped items to StatsPanel for BAB calculation
        var equippedItems = _equipmentSlots
            .Where(s => s.HasItem && s.EquippedItem?.Item != null)
            .Select(s => s.EquippedItem!.Item);
        StatsPanelContent.SetEquippedItems(equippedItems);

        CharacterPanelContent.SetFileType(_isBicFile);
        CharacterPanelContent.LoadCreature(creature);
        ClassesPanelContent.LoadCreature(creature);
        SkillsPanelContent.LoadCreature(creature);
        FeatsPanelContent.LoadCreature(creature);
        SpellsPanelContent.LoadCreature(creature);
        ScriptsPanelContent.LoadCreature(creature);
        AppearancePanelContent.LoadCreature(creature);
        AdvancedPanelContent.SetFileType(_isBicFile);
        AdvancedPanelContent.LoadCreature(creature);

        // Load QuickBar for BIC files only
        if (_isBicFile && creature is BicFile bicFile)
        {
            QuickBarPanelContent.LoadQuickBar(bicFile);
        }
        else
        {
            QuickBarPanelContent.ClearPanel();
        }

        // Update UI visibility based on file type
        UpdateFileTypeVisibility();
    }

    /// <summary>
    /// Update UI element visibility based on whether loaded file is BIC or UTC.
    /// BIC files (player characters) don't have scripts, conversation, or some advanced properties.
    /// UTC files (creature blueprints) don't have QuickBar, experience, gold, or age.
    /// </summary>
    private void UpdateFileTypeVisibility()
    {
        // Hide Scripts nav button for BIC files (player characters don't have scripts)
        NavScripts.IsVisible = !_isBicFile;

        // Show QuickBar nav button only for BIC files (player characters have quickbar)
        NavQuickBar.IsVisible = _isBicFile;

        // If currently on Scripts section and loading a BIC, navigate away
        if (_isBicFile && _currentSection == "Scripts")
        {
            NavigateToSection("Character", NavCharacter);
        }

        // If currently on QuickBar section and loading a UTC, navigate away
        if (!_isBicFile && _currentSection == "QuickBar")
        {
            NavigateToSection("Character", NavCharacter);
        }
    }

    private void ClearAllPanels()
    {
        // Reset file type for all panels that track it
        StatsPanelContent.SetFileType(false);
        StatsPanelContent.ClearStats();
        CharacterPanelContent.SetFileType(false);
        CharacterPanelContent.ClearPanel();
        ClassesPanelContent.ClearPanel();
        SkillsPanelContent.ClearPanel();
        FeatsPanelContent.ClearPanel();
        SpellsPanelContent.ClearPanel();
        ScriptsPanelContent.ClearPanel();
        AppearancePanelContent.ClearPanel();
        AdvancedPanelContent.SetFileType(false);
        AdvancedPanelContent.ClearPanel();
        QuickBarPanelContent.ClearPanel();

        // Reset nav button visibility to defaults (Scripts visible, QuickBar hidden)
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
            }
        }
    }

    #endregion

    #region Menu Handlers - Dialogs

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show(this);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        DialogHelper.ShowAboutDialog(this);
    }

    private async void OnLevelUpClick(object? sender, RoutedEventArgs e)
    {
        if (_currentCreature == null)
            return;

        var wizard = new LevelUpWizardWindow(_creatureDisplayService, _currentCreature);
        await wizard.ShowDialog(this);

        if (wizard.Confirmed)
        {
            // Refresh all panels to show updated data
            MarkDirty();
            LoadAllPanels(_currentCreature);
            UpdateCharacterHeader();
            UpdateStatus("Character leveled up");
        }
    }

    private void OnViewLevelHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (_currentCreature == null)
            return;

        var history = LevelHistoryService.Decode(_currentCreature.Comment);
        if (history == null || history.Count == 0)
        {
            DialogHelper.ShowMessageDialog(this, "Level History", "No level history recorded for this character.\n\nLevel history is recorded when you level up using Quartermaster's Level Up wizard.");
            return;
        }

        var formatted = LevelHistoryService.FormatForDisplay(
            history,
            _creatureDisplayService.GetClassName,
            _creatureDisplayService.GetFeatName,
            _creatureDisplayService.GetSkillName);

        DialogHelper.ShowMessageDialog(this, $"Level History ({history.Count} levels)", formatted);
    }

    private async void OnReLevelClick(object? sender, RoutedEventArgs e)
    {
        if (_currentCreature == null)
            return;

        int totalLevel = _currentCreature.ClassList.Sum(c => c.ClassLevel);
        if (totalLevel <= 1)
        {
            ShowErrorDialog("Cannot Re-Level", "Character is already at level 1.");
            return;
        }

        var firstClass = _currentCreature.ClassList.FirstOrDefault();
        var className = firstClass != null ? _creatureDisplayService.GetClassName(firstClass.Class) : "Unknown";

        var confirmed = await DialogHelper.ShowConfirmationDialog(
            this,
            "Re-Level Character",
            $"This will reset the character to level 1 {className}:\n\n" +
            $"• All class levels beyond 1 will be removed\n" +
            $"• All skills will be reset to 0\n" +
            $"• All choosable feats will be removed\n" +
            $"• Racial and class-granted feats will be kept\n\n" +
            $"After reset, use Level Up (Ctrl+L) to rebuild {totalLevel - 1} level(s).\n\n" +
            $"Continue?");

        if (!confirmed)
            return;

        // Strip character to level 1
        StripCharacterToLevelOne();

        MarkDirty();
        LoadAllPanels(_currentCreature);
        UpdateCharacterHeader();
        UpdateStatus($"Character reset to level 1. Use Level Up to rebuild.");
    }

    private void StripCharacterToLevelOne()
    {
        if (_currentCreature == null)
            return;

        // Keep first class at level 1, remove others
        if (_currentCreature.ClassList.Count > 0)
        {
            var firstClass = _currentCreature.ClassList[0];
            firstClass.ClassLevel = 1;
            _currentCreature.ClassList.Clear();
            _currentCreature.ClassList.Add(firstClass);
        }

        // Get feats to keep (racial + class granted at level 1)
        var featsToKeep = new HashSet<ushort>();

        // Racial feats
        var racialFeats = _creatureDisplayService.Feats.GetRaceGrantedFeatIds(_currentCreature.Race);
        foreach (var f in racialFeats)
            featsToKeep.Add((ushort)f);

        // Class granted feats at level 1
        if (_currentCreature.ClassList.Count > 0)
        {
            var classGrantedFeats = _creatureDisplayService.Feats.GetClassGrantedFeatIds(_currentCreature.ClassList[0].Class);
            foreach (var f in classGrantedFeats)
                featsToKeep.Add((ushort)f);
        }

        // Filter feat list to only keep granted feats
        var newFeatList = _currentCreature.FeatList.Where(f => featsToKeep.Contains(f)).ToList();
        _currentCreature.FeatList.Clear();
        foreach (var f in newFeatList)
            _currentCreature.FeatList.Add(f);

        // Reset all skills to 0
        for (int i = 0; i < _currentCreature.SkillList.Count; i++)
            _currentCreature.SkillList[i] = 0;

        // Clear known spells (will need to be re-selected)
        // Note: This is simplified - a full implementation would handle spell memorization differently
    }

    private async void OnDownLevelClick(object? sender, RoutedEventArgs e)
    {
        if (_currentCreature == null)
            return;

        int totalLevel = _currentCreature.ClassList.Sum(c => c.ClassLevel);
        if (totalLevel <= 1)
        {
            ShowErrorDialog("Cannot Down-Level", "Character is already at level 1.");
            return;
        }

        var firstClass = _currentCreature.ClassList.FirstOrDefault();
        var className = firstClass != null ? _creatureDisplayService.GetClassName(firstClass.Class) : "Unknown";
        var originalName = CreatureDisplayService.GetCreatureFullName(_currentCreature);

        var confirmed = await DialogHelper.ShowConfirmationDialog(
            this,
            "Down-Level Character",
            $"This will save a level 1 copy of \"{originalName}\" as a new file:\n\n" +
            $"• The copy will be level 1 {className}\n" +
            $"• All skills will be reset to 0\n" +
            $"• Only racial/class-granted feats will be kept\n" +
            $"• The original file will not be modified\n\n" +
            $"Choose where to save the level 1 copy.");

        if (!confirmed)
            return;

        // Show save dialog
        var filters = new List<Avalonia.Platform.Storage.FilePickerFileType>
        {
            new("NWN Creature") { Patterns = new[] { "*.utc" } },
            new("NWN Character") { Patterns = new[] { "*.bic" } },
            new("All Files") { Patterns = new[] { "*" } }
        };

        var suggestedName = $"{Path.GetFileNameWithoutExtension(_currentFilePath ?? "creature")}_lvl1{Path.GetExtension(_currentFilePath ?? ".utc")}";

        var storageProvider = StorageProvider;
        var result = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save Down-Leveled Copy",
            FileTypeChoices = filters,
            SuggestedFileName = suggestedName
        });

        if (result == null)
            return;

        var savePath = result.Path.LocalPath;

        try
        {
            // Create a deep copy (serialize and deserialize)
            var copy = CreateCreatureCopy(_currentCreature);

            // Strip the copy to level 1
            StripCreatureToLevelOne(copy);

            // Save the copy
            if (savePath.EndsWith(".bic", StringComparison.OrdinalIgnoreCase))
            {
                // BIC files wrap the UTC data in an additional layer
                // For simplicity, just save as UTC for now
                Radoub.Formats.Utc.UtcWriter.Write(copy, savePath);
            }
            else
            {
                Radoub.Formats.Utc.UtcWriter.Write(copy, savePath);
            }

            UpdateStatus($"Saved level 1 copy to {Path.GetFileName(savePath)}");
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Save Failed", $"Failed to save level 1 copy: {ex.Message}");
        }
    }

    private UtcFile CreateCreatureCopy(UtcFile original)
    {
        // Create a copy by serializing and deserializing
        var buffer = Radoub.Formats.Utc.UtcWriter.Write(original);
        return Radoub.Formats.Utc.UtcReader.Read(buffer);
    }

    private void StripCreatureToLevelOne(UtcFile creature)
    {
        // Keep first class at level 1, remove others
        if (creature.ClassList.Count > 0)
        {
            var firstClass = creature.ClassList[0];
            firstClass.ClassLevel = 1;
            creature.ClassList.Clear();
            creature.ClassList.Add(firstClass);
        }

        // Get feats to keep (racial + class granted at level 1)
        var featsToKeep = new HashSet<ushort>();

        // Racial feats
        var racialFeats = _creatureDisplayService.Feats.GetRaceGrantedFeatIds(creature.Race);
        foreach (var f in racialFeats)
            featsToKeep.Add((ushort)f);

        // Class granted feats at level 1
        if (creature.ClassList.Count > 0)
        {
            var classGrantedFeats = _creatureDisplayService.Feats.GetClassGrantedFeatIds(creature.ClassList[0].Class);
            foreach (var f in classGrantedFeats)
                featsToKeep.Add((ushort)f);
        }

        // Filter feat list to only keep granted feats
        var newFeatList = creature.FeatList.Where(f => featsToKeep.Contains(f)).ToList();
        creature.FeatList.Clear();
        foreach (var f in newFeatList)
            creature.FeatList.Add(f);

        // Reset all skills to 0
        for (int i = 0; i < creature.SkillList.Count; i++)
            creature.SkillList[i] = 0;
    }

    private void ShowErrorDialog(string title, string message)
    {
        DialogHelper.ShowErrorDialog(this, title, message);
    }

    #endregion

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #region Resource Resolution

    /// <summary>
    /// Resolve a conversation resref to a file path.
    /// Checks Override folder first, then module directory where creature was loaded.
    /// </summary>
    private string? ResolveConversationPath(string resRef)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        var dlgFilename = resRef + ".dlg";

        // Try to find in same directory as the loaded creature file
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            var creatureDir = Path.GetDirectoryName(_currentFilePath);
            if (!string.IsNullOrEmpty(creatureDir))
            {
                var localPath = Path.Combine(creatureDir, dlgFilename);
                if (File.Exists(localPath))
                    return localPath;
            }
        }

        // Try Override folder via game data service
        if (_gameDataService.IsConfigured)
        {
            var resourceInfo = _gameDataService.ListResources(ResourceTypes.Dlg)
                .FirstOrDefault(r => r.ResRef.Equals(resRef, StringComparison.OrdinalIgnoreCase)
                                     && r.Source == GameResourceSource.Override);

            if (resourceInfo?.SourcePath != null && File.Exists(resourceInfo.SourcePath))
                return resourceInfo.SourcePath;
        }

        return null;
    }

    #endregion
}
