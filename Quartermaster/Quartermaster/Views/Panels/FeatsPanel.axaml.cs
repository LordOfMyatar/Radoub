using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class FeatsPanel : UserControl
{
    private CreatureDisplayService? _displayService;
    private ItemIconService? _itemIconService;
    private UtcFile? _currentCreature;

    /// <summary>
    /// Raised when the creature's feat list is modified (feat added or removed).
    /// </summary>
    public event EventHandler? FeatsChanged;

    private TextBlock? _featsSummaryText;
    private TextBox? _searchTextBox;
    private Button? _clearSearchButton;
    private ComboBox? _categoryFilterComboBox;
    private ListBox? _featsList;
    private TextBlock? _noFeatsText;
    private TextBlock? _loadingText;
    private Expander? _specialAbilitiesExpander;
    private ItemsControl? _specialAbilitiesList;
    private TextBlock? _noAbilitiesText;

    private ObservableCollection<FeatListViewModel> _displayedFeats = new();
    private List<FeatListViewModel> _allFeats = new();
    private HashSet<ushort> _assignedFeatIds = new();
    private HashSet<int> _grantedFeatIds = new();
    private HashSet<int> _unavailableFeatIds = new();
    private ObservableCollection<SpecialAbilityViewModel> _abilities = new();

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
        _featsList = this.FindControl<ListBox>("FeatsList");
        _noFeatsText = this.FindControl<TextBlock>("NoFeatsText");
        _loadingText = this.FindControl<TextBlock>("LoadingText");
        _specialAbilitiesExpander = this.FindControl<Expander>("SpecialAbilitiesExpander");
        _specialAbilitiesList = this.FindControl<ItemsControl>("SpecialAbilitiesList");
        _noAbilitiesText = this.FindControl<TextBlock>("NoAbilitiesText");

        if (_featsList != null)
        {
            _featsList.ItemsSource = _displayedFeats;
            _featsList.AddHandler(Button.ClickEvent, OnFeatButtonClick);
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
    }

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

    public void LoadCreature(UtcFile? creature)
    {
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
    }

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

        // Get unavailable feats for this creature
        _unavailableFeatIds = _displayService.GetUnavailableFeatIds(creature, allFeatIds);

        foreach (var featId in allFeatIds)
        {
            _allFeats.Add(CreateFeatViewModel((ushort)featId, creature));
        }

        // Sort by name
        _allFeats = _allFeats.OrderBy(f => f.FeatName).ToList();
    }

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

        // Determine status display
        string statusIndicator;
        string statusText;
        IBrush statusColor;
        IBrush rowBackground;
        double textOpacity;

        if (isUnavailable && !isAssigned)
        {
            // Unavailable to this class/race
            statusIndicator = "✗";
            statusText = "Unavailable";
            statusColor = GetDisabledBrush();
            rowBackground = GetTransparentRowBackground(statusColor, 20);
            textOpacity = 0.5;
        }
        else if (isAssigned && isGranted)
        {
            statusIndicator = "★";
            statusText = "Granted";
            statusColor = GetSelectionBrush();
            rowBackground = GetTransparentRowBackground(statusColor, 30);
            textOpacity = 1.0;
        }
        else if (isAssigned)
        {
            statusIndicator = "✓";
            statusText = "Assigned";
            statusColor = GetSuccessBrush();
            rowBackground = GetTransparentRowBackground(statusColor, 30);
            textOpacity = 1.0;
        }
        else if (prereqResult != null && prereqResult.HasPrerequisites && !prereqResult.AllMet)
        {
            // Has unmet prerequisites
            statusIndicator = "⚠";
            statusText = "Prereqs";
            statusColor = GetWarningBrush();
            rowBackground = Brushes.Transparent;
            textOpacity = 0.7;
        }
        else if (prereqResult != null && prereqResult.HasPrerequisites && prereqResult.AllMet)
        {
            // All prerequisites met - available to select
            statusIndicator = "○";
            statusText = "Available";
            statusColor = GetInfoBrush();
            rowBackground = Brushes.Transparent;
            textOpacity = 0.8;
        }
        else
        {
            // No prerequisites
            statusIndicator = "";
            statusText = "";
            statusColor = Brushes.Transparent;
            rowBackground = Brushes.Transparent;
            textOpacity = 0.6;
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
            StatusIndicator = statusIndicator,
            StatusText = statusText,
            StatusColor = statusColor,
            RowBackground = rowBackground,
            TextOpacity = textOpacity
        };

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

    private void LoadSpecialAbilities(UtcFile creature)
    {
        foreach (var ability in creature.SpecAbilityList)
        {
            _abilities.Add(new SpecialAbilityViewModel
            {
                SpellId = ability.Spell,
                AbilityName = GetSpellNameInternal(ability.Spell),
                CasterLevel = ability.SpellCasterLevel,
                CasterLevelDisplay = $"CL {ability.SpellCasterLevel}",
                Flags = ability.SpellFlags
            });
        }

        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = _abilities.Count == 0;

        // Show expander if there are abilities
        if (_specialAbilitiesExpander != null && _abilities.Count > 0)
            _specialAbilitiesExpander.IsExpanded = true;
    }

    private void ApplySearchAndFilter()
    {
        if (_allFeats.Count == 0)
            return;

        var filtered = _allFeats.AsEnumerable();

        // Apply search filter
        var searchText = _searchTextBox?.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(searchText))
        {
            filtered = filtered.Where(f =>
                f.FeatName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply category filter
        int filterIndex = _categoryFilterComboBox?.SelectedIndex ?? 0;
        filtered = filterIndex switch
        {
            0 => filtered, // All Feats
            1 => filtered.Where(f => f.Category == FeatCategory.Combat),
            2 => filtered.Where(f => f.Category == FeatCategory.ActiveCombat),
            3 => filtered.Where(f => f.Category == FeatCategory.Defensive),
            4 => filtered.Where(f => f.Category == FeatCategory.Magical),
            5 => filtered.Where(f => f.Category == FeatCategory.ClassRacial),
            6 => filtered.Where(f => f.Category == FeatCategory.Other),
            7 => filtered.Where(f => f.IsAssigned), // Assigned Only
            8 => filtered.Where(f => f.IsGranted), // Granted Only
            9 => filtered.Where(f => !f.IsAssigned), // Unassigned Only
            10 => filtered.Where(f => !f.IsUnavailable && !f.IsAssigned), // Available Only
            11 => filtered.Where(f => f.IsUnavailable), // Unavailable Only
            12 => filtered.Where(f => f.HasPrerequisites && f.PrerequisitesMet && !f.IsAssigned), // Prereqs Met
            13 => filtered.Where(f => f.HasPrerequisites && !f.PrerequisitesMet), // Prereqs Unmet
            14 => filtered.Where(f => f.HasPrerequisites), // Has Prereqs
            _ => filtered
        };

        // Update display
        _displayedFeats.Clear();
        foreach (var feat in filtered)
        {
            _displayedFeats.Add(feat);
        }

        // Show "no feats" message if empty
        if (_noFeatsText != null)
            _noFeatsText.IsVisible = _displayedFeats.Count == 0;

        // Update summary with filter info
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var assignedCount = _assignedFeatIds.Count;
        var grantedCount = _grantedFeatIds.Count(g => _assignedFeatIds.Contains((ushort)g));
        var unavailableCount = _unavailableFeatIds.Count;
        var totalAvailable = _allFeats.Count;
        var displayedCount = _displayedFeats.Count;

        var filterNote = displayedCount < _allFeats.Count
            ? $" (showing {displayedCount} of {totalAvailable})"
            : "";

        SetText(_featsSummaryText,
            $"{assignedCount} assigned ({grantedCount} granted) | {unavailableCount} unavailable{filterNote}");
    }

    public void ClearPanel()
    {
        _displayedFeats.Clear();
        _allFeats.Clear();
        _assignedFeatIds.Clear();
        _grantedFeatIds.Clear();
        _unavailableFeatIds.Clear();
        _abilities.Clear();
        _currentCreature = null;

        SetText(_featsSummaryText, "0 feats assigned");
        if (_noFeatsText != null)
            _noFeatsText.IsVisible = false;
        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = true;
        if (_searchTextBox != null)
            _searchTextBox.Text = "";
        if (_categoryFilterComboBox != null)
            _categoryFilterComboBox.SelectedIndex = 0;
    }

    private void OnFeatButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (e.Source is not Button button)
            return;

        if (button.Tag is not ushort featId)
            return;

        if (_currentCreature == null)
            return;

        var automationId = button.GetValue(Avalonia.Automation.AutomationProperties.AutomationIdProperty);

        if (automationId == "AddFeatButton")
        {
            AddFeat(featId);
        }
        else if (automationId == "RemoveFeatButton")
        {
            RemoveFeat(featId);
        }
    }

    private void AddFeat(ushort featId)
    {
        if (_currentCreature == null)
            return;

        // Don't add if already assigned
        if (_currentCreature.FeatList.Contains(featId))
            return;

        // Add to creature's feat list
        _currentCreature.FeatList.Add(featId);
        _assignedFeatIds.Add(featId);

        // Refresh the display
        RefreshFeatDisplay(featId);

        // Notify listeners
        FeatsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveFeat(ushort featId)
    {
        if (_currentCreature == null)
            return;

        // Don't remove if not assigned
        if (!_currentCreature.FeatList.Contains(featId))
            return;

        // Don't remove granted feats
        if (_grantedFeatIds.Contains(featId))
            return;

        // Remove from creature's feat list
        _currentCreature.FeatList.Remove(featId);
        _assignedFeatIds.Remove(featId);

        // Refresh the display
        RefreshFeatDisplay(featId);

        // Notify listeners
        FeatsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshFeatDisplay(ushort featId)
    {
        if (_currentCreature == null)
            return;

        // Find and update the feat in _allFeats
        var index = _allFeats.FindIndex(f => f.FeatId == featId);
        if (index >= 0)
        {
            _allFeats[index] = CreateFeatViewModel(featId, _currentCreature);
        }

        // Re-apply filter to update displayed list
        ApplySearchAndFilter();

        // Update summary
        UpdateSummary();
    }

    private string GetFeatNameInternal(ushort featId)
    {
        if (_displayService != null)
            return _displayService.GetFeatName(featId);
        return $"Feat {featId}";
    }

    private string GetSpellNameInternal(ushort spellId)
    {
        if (_displayService != null)
            return _displayService.GetSpellName(spellId);
        return $"Spell {spellId}";
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }

    #region Theme-Aware Colors

    // Light theme default colors for fallback
    private static readonly IBrush DefaultDisabledBrush = new SolidColorBrush(Color.Parse("#757575")); // Gray
    private static readonly IBrush DefaultSuccessBrush = new SolidColorBrush(Color.Parse("#388E3C"));  // Green
    private static readonly IBrush DefaultWarningBrush = new SolidColorBrush(Color.Parse("#F57C00"));  // Orange
    private static readonly IBrush DefaultInfoBrush = new SolidColorBrush(Color.Parse("#1976D2"));     // Blue
    private static readonly IBrush DefaultSelectionBrush = new SolidColorBrush(Color.Parse("#FFC107")); // Gold/Yellow

    private IBrush GetDisabledBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeBorder", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultDisabledBrush;
    }

    private IBrush GetSuccessBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeSuccess", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultSuccessBrush;
    }

    private IBrush GetWarningBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeWarning", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultWarningBrush;
    }

    private IBrush GetInfoBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeInfo", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultInfoBrush;
    }

    private IBrush GetSelectionBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeSelection", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultSelectionBrush;
    }

    private static IBrush GetTransparentRowBackground(IBrush baseBrush, byte alpha = 30)
    {
        if (baseBrush is SolidColorBrush scb)
        {
            var c = scb.Color;
            return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        }
        return Brushes.Transparent;
    }

    #endregion
}

public class FeatListViewModel
{
    private Bitmap? _iconBitmap;
    private bool _iconLoaded = false;
    private ItemIconService? _iconService;

    public ushort FeatId { get; set; }
    public string FeatName { get; set; } = "";
    public string Description { get; set; } = "";
    public FeatCategory Category { get; set; }
    public string CategoryName { get; set; } = "";
    public bool IsAssigned { get; set; }
    public bool IsGranted { get; set; }
    public bool IsUnavailable { get; set; }
    public bool HasPrerequisites { get; set; }
    public bool PrerequisitesMet { get; set; }
    public string StatusIndicator { get; set; } = "";
    public string StatusText { get; set; } = "";
    public IBrush StatusColor { get; set; } = Brushes.Transparent;
    public IBrush RowBackground { get; set; } = Brushes.Transparent;
    public double TextOpacity { get; set; } = 1.0;

    /// <summary>
    /// Sets the icon service for lazy loading.
    /// </summary>
    public void SetIconService(ItemIconService? iconService)
    {
        _iconService = iconService;
    }

    /// <summary>
    /// Game icon for this feat (from feat.2da ICON column).
    /// Loaded lazily on first access.
    /// </summary>
    public Bitmap? IconBitmap
    {
        get
        {
            // Lazy load on first access
            if (!_iconLoaded && _iconService != null && _iconService.IsGameDataAvailable)
            {
                _iconLoaded = true;
                try
                {
                    _iconBitmap = _iconService.GetFeatIcon(FeatId);
                }
                catch
                {
                    // Silently fail - no icon
                }
            }
            return _iconBitmap;
        }
        set
        {
            _iconBitmap = value;
            _iconLoaded = true;
        }
    }

    /// <summary>
    /// Whether we have a real game icon (not placeholder).
    /// Returns true if icon service is available (assumes icon exists to avoid triggering load).
    /// </summary>
    public bool HasGameIcon => _iconService != null && _iconService.IsGameDataAvailable;

    /// <summary>
    /// Can this feat be added? (Not assigned and not granted)
    /// </summary>
    public bool CanAdd => !IsAssigned && !IsGranted;

    /// <summary>
    /// Can this feat be removed? (Assigned but not granted - can't remove class-granted feats)
    /// </summary>
    public bool CanRemove => IsAssigned && !IsGranted;
}

public class SpecialAbilityViewModel
{
    public ushort SpellId { get; set; }
    public string AbilityName { get; set; } = "";
    public byte CasterLevel { get; set; }
    public string CasterLevelDisplay { get; set; } = "";
    public byte Flags { get; set; }
}
