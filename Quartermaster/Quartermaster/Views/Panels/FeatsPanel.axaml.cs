using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class FeatsPanel : UserControl
{
    private CreatureDisplayService? _displayService;

    private TextBlock? _featsSummaryText;
    private TextBox? _searchTextBox;
    private Button? _clearSearchButton;
    private ComboBox? _categoryFilterComboBox;
    private ItemsControl? _featsList;
    private TextBlock? _noFeatsText;
    private TextBlock? _loadingText;
    private Expander? _specialAbilitiesExpander;
    private ItemsControl? _specialAbilitiesList;
    private TextBlock? _noAbilitiesText;

    private ObservableCollection<FeatListViewModel> _displayedFeats = new();
    private List<FeatListViewModel> _allFeats = new();
    private HashSet<ushort> _assignedFeatIds = new();
    private HashSet<int> _grantedFeatIds = new();
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
        _featsList = this.FindControl<ItemsControl>("FeatsList");
        _noFeatsText = this.FindControl<TextBlock>("NoFeatsText");
        _loadingText = this.FindControl<TextBlock>("LoadingText");
        _specialAbilitiesExpander = this.FindControl<Expander>("SpecialAbilitiesExpander");
        _specialAbilitiesList = this.FindControl<ItemsControl>("SpecialAbilitiesList");
        _noAbilitiesText = this.FindControl<TextBlock>("NoAbilitiesText");

        if (_featsList != null)
            _featsList.ItemsSource = _displayedFeats;
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

    public void LoadCreature(UtcFile? creature)
    {
        _displayedFeats.Clear();
        _allFeats.Clear();
        _assignedFeatIds.Clear();
        _grantedFeatIds.Clear();
        _abilities.Clear();

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
        LoadAllFeats();

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

    private void LoadAllFeats()
    {
        if (_displayService == null)
        {
            // Fallback: just show assigned feats
            foreach (var featId in _assignedFeatIds.OrderBy(f => GetFeatNameInternal(f)))
            {
                _allFeats.Add(CreateFeatViewModel(featId));
            }
            return;
        }

        // Get all feat IDs from feat.2da
        var allFeatIds = _displayService.GetAllFeatIds();

        foreach (var featId in allFeatIds)
        {
            _allFeats.Add(CreateFeatViewModel((ushort)featId));
        }

        // Sort by name
        _allFeats = _allFeats.OrderBy(f => f.FeatName).ToList();
    }

    private FeatListViewModel CreateFeatViewModel(ushort featId)
    {
        var isAssigned = _assignedFeatIds.Contains(featId);
        var isGranted = _grantedFeatIds.Contains(featId);
        var category = _displayService?.GetFeatCategory(featId) ?? FeatCategory.Other;
        var description = _displayService?.GetFeatDescription(featId) ?? "";

        // Determine status display
        string statusIndicator;
        string statusText;
        IBrush statusColor;
        IBrush rowBackground;
        double textOpacity;

        if (isAssigned && isGranted)
        {
            statusIndicator = "★";
            statusText = "Granted";
            statusColor = new SolidColorBrush(Colors.Gold);
            rowBackground = new SolidColorBrush(Color.FromArgb(30, 255, 215, 0)); // Light gold
            textOpacity = 1.0;
        }
        else if (isAssigned)
        {
            statusIndicator = "✓";
            statusText = "Assigned";
            statusColor = new SolidColorBrush(Colors.Green);
            rowBackground = new SolidColorBrush(Color.FromArgb(30, 0, 128, 0)); // Light green
            textOpacity = 1.0;
        }
        else
        {
            statusIndicator = "";
            statusText = "";
            statusColor = Brushes.Transparent;
            rowBackground = Brushes.Transparent;
            textOpacity = 0.6;
        }

        return new FeatListViewModel
        {
            FeatId = featId,
            FeatName = GetFeatNameInternal(featId),
            Description = description,
            Category = category,
            CategoryName = GetCategoryName(category),
            IsAssigned = isAssigned,
            IsGranted = isGranted,
            StatusIndicator = statusIndicator,
            StatusText = statusText,
            StatusColor = statusColor,
            RowBackground = rowBackground,
            TextOpacity = textOpacity
        };
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
            8 => filtered.Where(f => !f.IsAssigned), // Unassigned Only
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
        var totalAvailable = _allFeats.Count;
        var displayedCount = _displayedFeats.Count;

        var filterNote = displayedCount < _allFeats.Count
            ? $" (showing {displayedCount} of {totalAvailable})"
            : "";

        SetText(_featsSummaryText,
            $"{assignedCount} feats assigned ({grantedCount} granted by class){filterNote}");
    }

    public void ClearPanel()
    {
        _displayedFeats.Clear();
        _allFeats.Clear();
        _assignedFeatIds.Clear();
        _grantedFeatIds.Clear();
        _abilities.Clear();

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
}

public class FeatListViewModel
{
    public ushort FeatId { get; set; }
    public string FeatName { get; set; } = "";
    public string Description { get; set; } = "";
    public FeatCategory Category { get; set; }
    public string CategoryName { get; set; } = "";
    public bool IsAssigned { get; set; }
    public bool IsGranted { get; set; }
    public string StatusIndicator { get; set; } = "";
    public string StatusText { get; set; } = "";
    public IBrush StatusColor { get; set; } = Brushes.Transparent;
    public IBrush RowBackground { get; set; } = Brushes.Transparent;
    public double TextOpacity { get; set; } = 1.0;
}

public class SpecialAbilityViewModel
{
    public ushort SpellId { get; set; }
    public string AbilityName { get; set; } = "";
    public byte CasterLevel { get; set; }
    public string CasterLevelDisplay { get; set; } = "";
    public byte Flags { get; set; }
}
