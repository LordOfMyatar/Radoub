using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.UI.Services;
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
/// Special abilities are now in their own SpecialAbilitiesPanel.
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
    private CheckBox? _showChosenCheckBox;
    private CheckBox? _showGrantedCheckBox;
    private CheckBox? _showAvailableCheckBox;
    private CheckBox? _showPrereqsUnmetCheckBox;
    private CheckBox? _showUnavailableCheckBox;
    private ListBox? _featsList;
    private TextBlock? _noFeatsText;
    private TextBlock? _loadingText;
    private Border? _assignedFeatsListBorder;
    private StackPanel? _assignedFeatsListPanel;
    private bool _isLoading;

    private ObservableCollection<FeatListViewModel> _displayedFeats = new();
    private List<FeatListViewModel> _allFeats = new();
    private HashSet<ushort> _assignedFeatIds = new();
    private HashSet<int> _grantedFeatIds = new();
    private HashSet<int> _unavailableFeatIds = new();

    #endregion

    #region Events

    /// <summary>
    /// Raised when the creature's feat list is modified (feat added or removed).
    /// </summary>
    public event EventHandler? FeatsChanged;

    #endregion

    #region Constructor

    public FeatsPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _featsSummaryText = this.FindControl<TextBlock>("FeatsSummaryText");
        _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
        _clearSearchButton = this.FindControl<Button>("ClearSearchButton");
        _categoryFilterComboBox = this.FindControl<ComboBox>("CategoryFilterComboBox");
        _showChosenCheckBox = this.FindControl<CheckBox>("ShowChosenCheckBox");
        _showGrantedCheckBox = this.FindControl<CheckBox>("ShowGrantedCheckBox");
        _showAvailableCheckBox = this.FindControl<CheckBox>("ShowAvailableCheckBox");
        _showPrereqsUnmetCheckBox = this.FindControl<CheckBox>("ShowPrereqsUnmetCheckBox");
        _showUnavailableCheckBox = this.FindControl<CheckBox>("ShowUnavailableCheckBox");
        _featsList = this.FindControl<ListBox>("FeatsList");
        _noFeatsText = this.FindControl<TextBlock>("NoFeatsText");
        _loadingText = this.FindControl<TextBlock>("LoadingText");
        _assignedFeatsListBorder = this.FindControl<Border>("AssignedFeatsListBorder");
        _assignedFeatsListPanel = this.FindControl<StackPanel>("AssignedFeatsListPanel");

        if (_featsList != null)
        {
            _featsList.ItemsSource = _displayedFeats;
        }
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
        if (_showChosenCheckBox != null)
            _showChosenCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();
        if (_showGrantedCheckBox != null)
            _showGrantedCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();
        if (_showAvailableCheckBox != null)
            _showAvailableCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();
        if (_showPrereqsUnmetCheckBox != null)
            _showPrereqsUnmetCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();
        if (_showUnavailableCheckBox != null)
            _showUnavailableCheckBox.IsCheckedChanged += (s, e) => ApplySearchAndFilter();

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

        // Sort: assigned feats first, then alphabetical within each group
        _allFeats = _allFeats
            .OrderByDescending(f => f.IsAssigned)
            .ThenBy(f => f.FeatName)
            .ToList();
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
            statusColor = BrushManager.GetDisabledBrush(this);
            rowBackground = GetTransparentRowBackground(statusColor, 20);
            textOpacity = 0.5;
        }
        else if (isAssigned && isGranted)
        {
            statusText = "Granted";
            statusColor = BrushManager.GetWarningBrush(this);
            rowBackground = GetTransparentRowBackground(statusColor, 30);
            textOpacity = 1.0;
        }
        else if (isAssigned)
        {
            statusText = "Chosen";
            statusColor = BrushManager.GetSuccessBrush(this);
            rowBackground = GetTransparentRowBackground(statusColor, 30);
            textOpacity = 1.0;
        }
        else if (prereqResult != null && prereqResult.HasPrerequisites && !prereqResult.AllMet)
        {
            // Has unmet prerequisites
            statusText = "Prereqs Unmet";
            statusColor = BrushManager.GetWarningBrush(this);
            rowBackground = Brushes.Transparent;
            textOpacity = 0.7;
        }
        else if (prereqResult != null && prereqResult.HasPrerequisites && prereqResult.AllMet)
        {
            // All prerequisites met - available to select
            statusText = "Available";
            statusColor = BrushManager.GetInfoBrush(this);
            rowBackground = Brushes.Transparent;
            textOpacity = 0.8;
        }
        else
        {
            // No prerequisites - available to select
            statusText = "Available";
            statusColor = BrushManager.GetInfoBrush(this);
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
