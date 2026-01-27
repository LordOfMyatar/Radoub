using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Quartermaster.Services;
using Quartermaster.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

/// <summary>
/// Panel for displaying and editing creature feats and special abilities.
/// Split into partial classes for maintainability:
/// - FeatsPanel.axaml.cs (this file): Core initialization, loading, and ViewModel creation
/// - FeatsPanel.Search.cs: Search and filter functionality
/// - FeatsPanel.Display.cs: Summary display, assigned feats list, theme helpers
/// - FeatsPanel.Selection.cs: Feat add/remove operations
/// - FeatsPanel.SpecialAbilities.cs: Special abilities management
/// </summary>
public partial class FeatsPanel : UserControl
{
    #region Fields

    private CreatureDisplayService? _displayService;
    private ItemIconService? _itemIconService;
    private UtcFile? _currentCreature;

    private TextBlock? _featsSummaryText;
    private TextBox? _searchTextBox;
    private Button? _clearSearchButton;
    private ComboBox? _categoryFilterComboBox;
    private CheckBox? _showAssignedCheckBox;
    private CheckBox? _showGrantedCheckBox;
    private CheckBox? _showAvailableCheckBox;
    private CheckBox? _showPrereqsUnmetCheckBox;
    private CheckBox? _showUnavailableCheckBox;
    private ListBox? _featsList;
    private TextBlock? _noFeatsText;
    private TextBlock? _loadingText;
    private Expander? _specialAbilitiesExpander;
    private ItemsControl? _specialAbilitiesList;
    private TextBlock? _noAbilitiesText;
    private Button? _addAbilityButton;
    private Border? _assignedFeatsListBorder;
    private StackPanel? _assignedFeatsListPanel;
    private bool _isLoading;

    private ObservableCollection<FeatListViewModel> _displayedFeats = new();
    private List<FeatListViewModel> _allFeats = new();
    private HashSet<ushort> _assignedFeatIds = new();
    private HashSet<int> _grantedFeatIds = new();
    private HashSet<int> _unavailableFeatIds = new();
    private ObservableCollection<SpecialAbilityViewModel> _abilities = new();

    #endregion

    #region Events

    /// <summary>
    /// Raised when the creature's feat list is modified (feat added or removed).
    /// </summary>
    public event EventHandler? FeatsChanged;

    /// <summary>
    /// Raised when the creature's special abilities are modified.
    /// </summary>
    public event EventHandler? SpecialAbilitiesChanged;

    #endregion

    #region Constructor

    public FeatsPanel()
    {
        InitializeComponent();

        // Subscribe to theme changes to refresh color-dependent view models
        SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.CurrentThemeId) ||
            e.PropertyName == nameof(SettingsService.FontFamily))
        {
            // Theme or font changed - reload creature to refresh view
            if (_currentCreature != null)
            {
                LoadCreature(_currentCreature);
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _featsSummaryText = this.FindControl<TextBlock>("FeatsSummaryText");
        _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
        _clearSearchButton = this.FindControl<Button>("ClearSearchButton");
        _categoryFilterComboBox = this.FindControl<ComboBox>("CategoryFilterComboBox");
        _showAssignedCheckBox = this.FindControl<CheckBox>("ShowAssignedCheckBox");
        _showGrantedCheckBox = this.FindControl<CheckBox>("ShowGrantedCheckBox");
        _showAvailableCheckBox = this.FindControl<CheckBox>("ShowAvailableCheckBox");
        _showPrereqsUnmetCheckBox = this.FindControl<CheckBox>("ShowPrereqsUnmetCheckBox");
        _showUnavailableCheckBox = this.FindControl<CheckBox>("ShowUnavailableCheckBox");
        _featsList = this.FindControl<ListBox>("FeatsList");
        _noFeatsText = this.FindControl<TextBlock>("NoFeatsText");
        _loadingText = this.FindControl<TextBlock>("LoadingText");
        _specialAbilitiesExpander = this.FindControl<Expander>("SpecialAbilitiesExpander");
        _specialAbilitiesList = this.FindControl<ItemsControl>("SpecialAbilitiesList");
        _noAbilitiesText = this.FindControl<TextBlock>("NoAbilitiesText");
        _addAbilityButton = this.FindControl<Button>("AddAbilityButton");
        _assignedFeatsListBorder = this.FindControl<Border>("AssignedFeatsListBorder");
        _assignedFeatsListPanel = this.FindControl<StackPanel>("AssignedFeatsListPanel");

        if (_featsList != null)
        {
            _featsList.ItemsSource = _displayedFeats;
        }
        if (_specialAbilitiesList != null)
            _specialAbilitiesList.ItemsSource = _abilities;

        // Wire up search and filter controls
        if (_searchTextBox != null)
        {
            _searchTextBox.TextChanged += (s, e) => ApplySearchAndFilter();
        }

        if (_clearSearchButton != null)
        {
            _clearSearchButton.Click += (s, e) =>
            {
                if (_searchTextBox != null)
                    _searchTextBox.Text = "";
            };
        }

        if (_categoryFilterComboBox != null)
        {
            _categoryFilterComboBox.SelectionChanged += (s, e) => ApplySearchAndFilter();
        }

        // Wire up status filter checkboxes
        if (_showAssignedCheckBox != null)
            _showAssignedCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();
        if (_showGrantedCheckBox != null)
            _showGrantedCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();
        if (_showAvailableCheckBox != null)
            _showAvailableCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();
        if (_showPrereqsUnmetCheckBox != null)
            _showPrereqsUnmetCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();
        if (_showUnavailableCheckBox != null)
            _showUnavailableCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();

        // Wire up Add Ability button
        if (_addAbilityButton != null)
            _addAbilityButton.Click += OnAddAbilityClick;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the display service for 2DA/TLK lookups.
    /// </summary>
    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
    }

    /// <summary>
    /// Sets the icon service for loading feat icons.
    /// </summary>
    public void SetIconService(ItemIconService iconService)
    {
        _itemIconService = iconService;
        UnifiedLogger.LogUI(LogLevel.INFO, $"FeatsPanel: IconService set, IsGameDataAvailable={iconService?.IsGameDataAvailable}");
    }

    /// <summary>
    /// Loads a creature's feats and special abilities into the panel.
    /// </summary>
    public void LoadCreature(UtcFile? creature)
    {
        _isLoading = true;

        _displayedFeats.Clear();
        _allFeats.Clear();
        _assignedFeatIds.Clear();
        _grantedFeatIds.Clear();
        _unavailableFeatIds.Clear();
        _abilities.Clear();
        _currentCreature = creature;

        if (creature == null)
        {
            ClearPanel();
            _isLoading = false;
            return;
        }

        // Show loading state
        if (_loadingText != null)
            _loadingText.IsVisible = true;

        // Get creature's assigned feats
        _assignedFeatIds = new HashSet<ushort>(creature.FeatList);

        // Get granted feats from class levels
        if (_displayService != null)
        {
            _grantedFeatIds = _displayService.GetCombinedGrantedFeatIds(creature);
        }

        // Load ALL feats from feat.2da
        LoadAllFeats(creature);

        // Load special abilities
        LoadSpecialAbilities(creature);

        // Apply initial filter
        ApplySearchAndFilter();

        // Update summary
        UpdateSummary();

        // Hide loading state
        if (_loadingText != null)
            _loadingText.IsVisible = false;

        _isLoading = false;
    }

    #endregion

    #region Feat Loading

    /// <summary>
    /// Loads all feats from feat.2da and creates view models.
    /// </summary>
    private void LoadAllFeats(UtcFile creature)
    {
        if (_displayService == null)
        {
            // Fallback: just show assigned feats
            foreach (var featId in _assignedFeatIds.OrderBy(f => GetFeatNameInternal(f)))
            {
                _allFeats.Add(CreateFeatViewModel(featId, creature));
            }
            return;
        }

        // Get all feat IDs from feat.2da
        var allFeatIds = _displayService.GetAllFeatIds();
        var allFeatIdsSet = new HashSet<int>(allFeatIds);

        // Get unavailable feats for this creature
        _unavailableFeatIds = _displayService.GetUnavailableFeatIds(creature, allFeatIds);

        foreach (var featId in allFeatIds)
        {
            _allFeats.Add(CreateFeatViewModel((ushort)featId, creature));
        }

        // Ensure all assigned feats are included, even if not in feat.2da
        // (handles custom content or feats with missing 2DA entries)
        foreach (var assignedFeatId in _assignedFeatIds)
        {
            if (!allFeatIdsSet.Contains(assignedFeatId))
            {
                _allFeats.Add(CreateFeatViewModel(assignedFeatId, creature));
            }
        }

        // Sort by name
        _allFeats = _allFeats.OrderBy(f => f.FeatName).ToList();
    }

    /// <summary>
    /// Creates a FeatListViewModel for a single feat.
    /// </summary>
    private FeatListViewModel CreateFeatViewModel(ushort featId, UtcFile creature)
    {
        var isAssigned = _assignedFeatIds.Contains(featId);
        var isGranted = _grantedFeatIds.Contains(featId);
        var isUnavailable = _unavailableFeatIds.Contains(featId);
        var category = _displayService?.GetFeatCategory(featId) ?? FeatCategory.Other;
        var description = _displayService?.GetFeatDescription(featId) ?? "";

        // Check prerequisites
        FeatPrereqResult? prereqResult = null;
        if (_displayService != null)
        {
            prereqResult = _displayService.CheckFeatPrerequisites(creature, featId, _assignedFeatIds);
        }

        // Build tooltip with description and prerequisites
        var tooltip = BuildTooltip(description, prereqResult, isUnavailable);

        // Determine status display - always show a status
        string statusText;
        IBrush statusColor;
        IBrush rowBackground;
        double textOpacity;

        if (isUnavailable && !isAssigned)
        {
            // Unavailable to this class/race
            statusText = "Unavailable";
            statusColor = GetDisabledBrush();
            rowBackground = GetTransparentRowBackground(statusColor, 20);
            textOpacity = 0.5;
        }
        else if (isAssigned && isGranted)
        {
            statusText = "Granted";
            statusColor = GetSelectionBrush();
            rowBackground = GetTransparentRowBackground(statusColor, 30);
            textOpacity = 1.0;
        }
        else if (isAssigned)
        {
            statusText = "Assigned";
            statusColor = GetSuccessBrush();
            rowBackground = GetTransparentRowBackground(statusColor, 30);
            textOpacity = 1.0;
        }
        else if (prereqResult != null && prereqResult.HasPrerequisites && !prereqResult.AllMet)
        {
            // Has unmet prerequisites
            statusText = "Prereqs Unmet";
            statusColor = GetWarningBrush();
            rowBackground = Brushes.Transparent;
            textOpacity = 0.7;
        }
        else if (prereqResult != null && prereqResult.HasPrerequisites && prereqResult.AllMet)
        {
            // All prerequisites met - available to select
            statusText = "Available";
            statusColor = GetInfoBrush();
            rowBackground = Brushes.Transparent;
            textOpacity = 0.8;
        }
        else
        {
            // No prerequisites - available to select
            statusText = "Available";
            statusColor = GetInfoBrush();
            rowBackground = Brushes.Transparent;
            textOpacity = 0.8;
        }

        var vm = new FeatListViewModel
        {
            FeatId = featId,
            FeatName = GetFeatNameInternal(featId),
            Description = tooltip,
            Category = category,
            CategoryName = GetCategoryName(category),
            IsAssigned = isAssigned,
            IsGranted = isGranted,
            IsUnavailable = isUnavailable,
            HasPrerequisites = prereqResult?.HasPrerequisites ?? false,
            PrerequisitesMet = prereqResult?.AllMet ?? true,
            StatusText = statusText,
            StatusColor = statusColor,
            RowBackground = rowBackground,
            TextOpacity = textOpacity
        };

        // Wire up change handler
        vm.OnAssignedChanged = OnFeatAssignedChanged;

        // Load feat icon if available
        LoadFeatIcon(vm);

        return vm;
    }

    /// <summary>
    /// Loads the game icon for a feat from feat.2da ICON column.
    /// Icons are loaded lazily when binding requests them.
    /// </summary>
    private void LoadFeatIcon(FeatListViewModel featVm)
    {
        // Don't load upfront - use lazy loading via IconBitmap getter
        featVm.SetIconService(_itemIconService);
    }

    private static string BuildTooltip(string description, FeatPrereqResult? prereqResult, bool isUnavailable)
    {
        var lines = new List<string>();

        if (!string.IsNullOrEmpty(description))
        {
            lines.Add(description);
        }

        if (isUnavailable)
        {
            lines.Add("");
            lines.Add("⚠ Not available to this class/race");
        }

        if (prereqResult != null && prereqResult.HasPrerequisites)
        {
            lines.Add("");
            lines.Add(prereqResult.GetTooltip());
        }

        return string.Join("\n", lines);
    }

    private static string GetCategoryName(FeatCategory category)
    {
        return category switch
        {
            FeatCategory.Combat => "Combat",
            FeatCategory.ActiveCombat => "Active Combat",
            FeatCategory.Defensive => "Defensive",
            FeatCategory.Magical => "Magical",
            FeatCategory.ClassRacial => "Class/Racial",
            FeatCategory.Other => "Other",
            _ => "Other"
        };
    }

    #endregion
}
