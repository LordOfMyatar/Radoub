using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Quartermaster.Views.Panels;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Quartermaster.Views.Dialogs;
using Quartermaster.Views.Helpers;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;
using Radoub.UI.Controls;
using Radoub.UI.ViewModels;
using System;
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
        StatsPanelContent.ChallengeRatingChanged += (s, e) => MarkDirty();
        StatsPanelContent.AbilityScoresChanged += (s, e) => MarkDirty();
        StatsPanelContent.HitPointsChanged += (s, e) => MarkDirty();
        StatsPanelContent.NaturalAcChanged += (s, e) => MarkDirty();

        // Initialize character panel with display service
        CharacterPanelContent.SetDisplayService(_creatureDisplayService);
        CharacterPanelContent.SetConversationResolver(ResolveConversationPath);
        CharacterPanelContent.CharacterChanged += (s, e) => MarkDirty();

        // Initialize classes panel with display service for 2DA/TLK lookups
        ClassesPanelContent.SetDisplayService(_creatureDisplayService);
        ClassesPanelContent.AlignmentChanged += (s, e) => MarkDirty();
        ClassesPanelContent.PackageChanged += (s, e) => MarkDirty();

        // Initialize feats panel with display service for 2DA/TLK lookups
        FeatsPanelContent.SetDisplayService(_creatureDisplayService);

        // Initialize skills panel with display service for 2DA/TLK lookups
        SkillsPanelContent.SetDisplayService(_creatureDisplayService);

        // Initialize spells panel with display service for 2DA/TLK lookups
        SpellsPanelContent.SetDisplayService(_creatureDisplayService);
        SpellsPanelContent.SpellsChanged += (s, e) => MarkDirty();

        // Initialize appearance panel with display service and palette colors
        AppearancePanelContent.SetDisplayService(_creatureDisplayService);
        AppearancePanelContent.SetPaletteColorService(new PaletteColorService(_gameDataService));
        AppearancePanelContent.AppearanceChanged += (s, e) => MarkDirty();

        // Initialize advanced panel with display service
        AdvancedPanelContent.SetDisplayService(_creatureDisplayService);
        AdvancedPanelContent.TagChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.CommentChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.FlagsChanged += (s, e) => MarkDirty();
        AdvancedPanelContent.BehaviorChanged += (s, e) => MarkDirty();

        // Initialize scripts panel with conversation resolver and game data service
        ScriptsPanelContent.SetConversationResolver(ResolveConversationPath);
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

        // Start loading game items in background immediately
        StartGameItemsLoad();

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
        if (_currentCreature == null)
        {
            CharacterNameText.Text = "No Character Loaded";
            CharacterSummaryText.Text = "";
            return;
        }

        // Get character name using display service
        CharacterNameText.Text = CreatureDisplayService.GetCreatureFullName(_currentCreature);

        // Build race/class summary using display service
        CharacterSummaryText.Text = _creatureDisplayService.GetCreatureSummary(_currentCreature);
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

    private void MarkDirty()
    {
        // Don't mark dirty during file loading - panels may fire change events
        if (_isLoading) return;

        if (!_isDirty)
        {
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

        // Update UI visibility based on file type
        UpdateFileTypeVisibility();
    }

    /// <summary>
    /// Update UI element visibility based on whether loaded file is BIC or UTC.
    /// BIC files (player characters) don't have scripts, conversation, or some advanced properties.
    /// </summary>
    private void UpdateFileTypeVisibility()
    {
        // Hide Scripts nav button for BIC files (player characters don't have scripts)
        NavScripts.IsVisible = !_isBicFile;

        // If currently on Scripts section and loading a BIC, navigate away
        if (_isBicFile && _currentSection == "Scripts")
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

        // Restore Scripts nav button visibility (show by default)
        NavScripts.IsVisible = true;
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

    #region Menu Handlers - Dialogs

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        await settingsWindow.ShowDialog(this);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        DialogHelper.ShowAboutDialog(this);
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        await DialogHelper.ShowErrorDialog(this, title, message);
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
