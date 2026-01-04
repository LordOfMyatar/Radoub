using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class SkillsPanel : UserControl
{
    private CreatureDisplayService? _displayService;
    private ItemIconService? _itemIconService;

    private TextBlock? _skillsSummaryText;
    private ItemsControl? _skillsList;
    private TextBlock? _noSkillsText;
    private ComboBox? _sortComboBox;
    private CheckBox? _trainedOnlyCheckBox;

    private ObservableCollection<SkillViewModel> _skills = new();
    private List<SkillViewModel> _allSkills = new(); // Unfiltered list
    private HashSet<int> _classSkillIds = new();
    private HashSet<int> _unavailableSkillIds = new();
    private UtcFile? _currentCreature;

    public SkillsPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _skillsSummaryText = this.FindControl<TextBlock>("SkillsSummaryText");
        _skillsList = this.FindControl<ItemsControl>("SkillsList");
        _noSkillsText = this.FindControl<TextBlock>("NoSkillsText");
        _sortComboBox = this.FindControl<ComboBox>("SortComboBox");
        _trainedOnlyCheckBox = this.FindControl<CheckBox>("TrainedOnlyCheckBox");

        if (_skillsList != null)
            _skillsList.ItemsSource = _skills;

        // Wire up sort/filter controls
        if (_sortComboBox != null)
        {
            _sortComboBox.SelectionChanged += (s, e) => ApplySortAndFilter();
        }

        if (_trainedOnlyCheckBox != null)
        {
            _trainedOnlyCheckBox.IsCheckedChanged += (s, e) => ApplySortAndFilter();
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
    /// Sets the icon service for loading skill icons.
    /// </summary>
    public void SetIconService(ItemIconService iconService)
    {
        _itemIconService = iconService;
        UnifiedLogger.LogUI(LogLevel.INFO, $"SkillsPanel: IconService set, IsGameDataAvailable={iconService?.IsGameDataAvailable}");
    }

    public void LoadCreature(UtcFile? creature)
    {
        _skills.Clear();
        _allSkills.Clear();
        _classSkillIds.Clear();
        _unavailableSkillIds.Clear();
        _currentCreature = creature;

        if (creature == null || creature.SkillList.Count == 0)
        {
            ClearPanel();
            return;
        }

        // Get combined class skills and unavailable skills from all character classes
        if (_displayService != null)
        {
            _classSkillIds = _displayService.GetCombinedClassSkillIds(creature);
            _unavailableSkillIds = _displayService.GetUnavailableSkillIds(creature, creature.SkillList.Count);
        }

        // Load all skills
        for (int i = 0; i < creature.SkillList.Count; i++)
        {
            var ranks = creature.SkillList[i];
            var skillName = GetSkillName(i);
            var keyAbility = GetSkillKeyAbility(i);
            var isClassSkill = _classSkillIds.Contains(i);
            var isUnavailable = _unavailableSkillIds.Contains(i);

            // Calculate ability modifier for total
            var abilityModifier = GetAbilityModifier(creature, keyAbility);
            var total = ranks + abilityModifier;

            // Determine row background and text opacity
            IBrush rowBackground;
            double textOpacity;
            if (isUnavailable)
            {
                rowBackground = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)); // Gray for unavailable
                textOpacity = 0.5;
            }
            else if (isClassSkill)
            {
                rowBackground = new SolidColorBrush(Color.FromArgb(30, 100, 149, 237)); // Light blue for class skills
                textOpacity = 1.0;
            }
            else
            {
                rowBackground = Brushes.Transparent;
                textOpacity = 1.0;
            }

            var vm = new SkillViewModel
            {
                SkillId = i,
                SkillName = skillName,
                KeyAbility = keyAbility,
                Ranks = ranks,
                RanksDisplay = ranks.ToString(),
                AbilityModifier = abilityModifier,
                Total = total,
                TotalDisplay = FormatTotal(total, abilityModifier),
                IsClassSkill = isClassSkill,
                IsUnavailable = isUnavailable,
                ClassSkillIndicator = isUnavailable ? "✗" : (isClassSkill ? "●" : "○"),
                RowBackground = rowBackground,
                TextOpacity = textOpacity
            };

            // Load skill icon if available
            LoadSkillIcon(vm);

            _allSkills.Add(vm);
        }

        // Apply initial sort and filter
        ApplySortAndFilter();

        // Update summary
        UpdateSummary();

        if (_noSkillsText != null)
            _noSkillsText.IsVisible = false;
    }

    private int GetAbilityModifier(UtcFile creature, string keyAbility)
    {
        // Get the base ability score + racial modifier, then calculate modifier
        int abilityScore = keyAbility.ToUpperInvariant() switch
        {
            "STR" => creature.Str,
            "DEX" => creature.Dex,
            "CON" => creature.Con,
            "INT" => creature.Int,
            "WIS" => creature.Wis,
            "CHA" => creature.Cha,
            _ => 10
        };

        // Standard D&D formula: (score - 10) / 2
        return CreatureDisplayService.CalculateAbilityBonus(abilityScore);
    }

    private static string FormatTotal(int total, int abilityModifier)
    {
        // Show total with modifier breakdown in tooltip-friendly format
        var sign = abilityModifier >= 0 ? "+" : "";
        return $"{total}";
    }

    public void ClearPanel()
    {
        _skills.Clear();
        _allSkills.Clear();
        _classSkillIds.Clear();
        _unavailableSkillIds.Clear();
        _currentCreature = null;
        SetText(_skillsSummaryText, "0 skills with ranks");
        if (_noSkillsText != null)
            _noSkillsText.IsVisible = true;
    }

    private void ApplySortAndFilter()
    {
        if (_allSkills.Count == 0)
            return;

        var filtered = _allSkills.AsEnumerable();

        // Apply "trained only" filter
        bool trainedOnly = _trainedOnlyCheckBox?.IsChecked ?? false;
        if (trainedOnly)
        {
            filtered = filtered.Where(s => s.Ranks > 0);
        }

        // Apply sort
        int sortIndex = _sortComboBox?.SelectedIndex ?? 0;
        filtered = sortIndex switch
        {
            0 => filtered.OrderBy(s => s.SkillName),                          // Alphabetical
            1 => filtered.OrderByDescending(s => s.Ranks).ThenBy(s => s.SkillName), // By Rank (descending)
            2 => filtered.OrderByDescending(s => s.IsClassSkill).ThenBy(s => s.SkillName), // Class skills first
            _ => filtered.OrderBy(s => s.SkillName)
        };

        // Update display
        _skills.Clear();
        foreach (var skill in filtered)
        {
            _skills.Add(skill);
        }

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var skillsWithRanks = _allSkills.Count(s => s.Ranks > 0);
        var totalRanks = _allSkills.Sum(s => s.Ranks);
        var classSkillCount = _classSkillIds.Count;

        var displayedCount = _skills.Count;
        var filterNote = displayedCount < _allSkills.Count ? $" (showing {displayedCount})" : "";

        SetText(_skillsSummaryText,
            $"{skillsWithRanks} skills with ranks ({totalRanks} total) | {classSkillCount} class skills{filterNote}");
    }

    private string GetSkillName(int skillId)
    {
        if (_displayService != null)
            return _displayService.GetSkillName(skillId);

        // Fallback to hardcoded names
        return skillId switch
        {
            0 => "Animal Empathy",
            1 => "Concentration",
            2 => "Disable Trap",
            3 => "Discipline",
            4 => "Heal",
            5 => "Hide",
            6 => "Listen",
            7 => "Lore",
            8 => "Move Silently",
            9 => "Open Lock",
            10 => "Parry",
            11 => "Perform",
            12 => "Persuade",
            13 => "Pick Pocket",
            14 => "Search",
            15 => "Set Trap",
            16 => "Spellcraft",
            17 => "Spot",
            18 => "Taunt",
            19 => "Use Magic Device",
            20 => "Appraise",
            21 => "Tumble",
            22 => "Craft Trap",
            23 => "Bluff",
            24 => "Intimidate",
            25 => "Craft Armor",
            26 => "Craft Weapon",
            27 => "Ride",
            _ => $"Skill {skillId}"
        };
    }

    private string GetSkillKeyAbility(int skillId)
    {
        if (_displayService != null)
            return _displayService.GetSkillKeyAbility(skillId);

        // Fallback to hardcoded values
        return skillId switch
        {
            0 => "CHA",  // Animal Empathy
            1 => "CON",  // Concentration
            2 => "INT",  // Disable Trap
            3 => "STR",  // Discipline
            4 => "WIS",  // Heal
            5 => "DEX",  // Hide
            6 => "WIS",  // Listen
            7 => "INT",  // Lore
            8 => "DEX",  // Move Silently
            9 => "DEX",  // Open Lock
            10 => "DEX", // Parry
            11 => "CHA", // Perform
            12 => "CHA", // Persuade
            13 => "DEX", // Pick Pocket
            14 => "INT", // Search
            15 => "DEX", // Set Trap
            16 => "INT", // Spellcraft
            17 => "WIS", // Spot
            18 => "CHA", // Taunt
            19 => "CHA", // Use Magic Device
            20 => "INT", // Appraise
            21 => "DEX", // Tumble
            22 => "INT", // Craft Trap
            23 => "CHA", // Bluff
            24 => "CHA", // Intimidate
            25 => "INT", // Craft Armor
            26 => "INT", // Craft Weapon
            27 => "DEX", // Ride
            _ => "INT"
        };
    }

    /// <summary>
    /// Loads the game icon for a skill from skills.2da Icon column.
    /// Icons are loaded lazily when binding requests them.
    /// </summary>
    private void LoadSkillIcon(SkillViewModel skillVm)
    {
        // Don't load upfront - use lazy loading via IconBitmap getter
        skillVm.SetIconService(_itemIconService);
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}

public class SkillViewModel
{
    private Bitmap? _iconBitmap;
    private bool _iconLoaded = false;
    private ItemIconService? _iconService;

    public int SkillId { get; set; }
    public string SkillName { get; set; } = "";
    public string KeyAbility { get; set; } = "";
    public int Ranks { get; set; }
    public string RanksDisplay { get; set; } = "0";
    public int AbilityModifier { get; set; }
    public int Total { get; set; }
    public string TotalDisplay { get; set; } = "0";
    public bool IsClassSkill { get; set; }
    public bool IsUnavailable { get; set; }
    public string ClassSkillIndicator { get; set; } = "○";
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
    /// Game icon for this skill (from skills.2da Icon column).
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
                    _iconBitmap = _iconService.GetSkillIcon(SkillId);
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
}
