using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class SkillsPanel : BasePanelControl
{
    private CreatureDisplayService? _displayService;
    private ItemIconService? _itemIconService;

    private TextBlock? _skillsSummaryText;
    private ItemsControl? _skillsList;
    private TextBlock? _noSkillsText;
    private ComboBox? _sortComboBox;
    private CheckBox? _trainedOnlyCheckBox;

    private ObservableCollection<SkillViewModel> _skills = new();
    private List<SkillViewModel> _allSkills = new();
    private HashSet<int> _classSkillIds = new();
    private HashSet<int> _unavailableSkillIds = new();

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

        if (_sortComboBox != null)
            _sortComboBox.SelectionChanged += (s, e) => ApplySortAndFilter();

        if (_trainedOnlyCheckBox != null)
            _trainedOnlyCheckBox.IsCheckedChanged += (s, e) => ApplySortAndFilter();
    }

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
    }

    public void SetIconService(ItemIconService iconService)
    {
        _itemIconService = iconService;
        UnifiedLogger.LogUI(LogLevel.INFO, $"SkillsPanel: IconService set, IsGameDataAvailable={iconService?.IsGameDataAvailable}");
    }

    public override void LoadCreature(UtcFile? creature)
    {
        _skills.Clear();
        _allSkills.Clear();
        _classSkillIds.Clear();
        _unavailableSkillIds.Clear();
        CurrentCreature = creature;

        if (creature == null || creature.SkillList.Count == 0)
        {
            ClearPanel();
            return;
        }

        if (_displayService != null)
        {
            _classSkillIds = _displayService.GetCombinedClassSkillIds(creature);
            _unavailableSkillIds = _displayService.GetUnavailableSkillIds(creature, creature.SkillList.Count);
        }

        for (int i = 0; i < creature.SkillList.Count; i++)
        {
            var ranks = creature.SkillList[i];
            var skillName = GetSkillName(i);
            var keyAbility = GetSkillKeyAbility(i);
            var isClassSkill = _classSkillIds.Contains(i);
            var isUnavailable = _unavailableSkillIds.Contains(i);

            var abilityModifier = GetAbilityModifier(creature, keyAbility);
            var total = ranks + abilityModifier;

            IBrush rowBackground;
            double textOpacity;
            if (isUnavailable)
            {
                rowBackground = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128));
                textOpacity = 0.5;
            }
            else if (isClassSkill)
            {
                rowBackground = new SolidColorBrush(Color.FromArgb(30, 100, 149, 237));
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
                TotalDisplay = total.ToString(),
                IsClassSkill = isClassSkill,
                IsUnavailable = isUnavailable,
                ClassSkillIndicator = isUnavailable ? "✗" : (isClassSkill ? "●" : "○"),
                RowBackground = rowBackground,
                TextOpacity = textOpacity
            };

            vm.SetIconService(_itemIconService);
            _allSkills.Add(vm);
        }

        ApplySortAndFilter();
        UpdateSummary();

        if (_noSkillsText != null)
            _noSkillsText.IsVisible = false;
    }

    private int GetAbilityModifier(UtcFile creature, string keyAbility)
    {
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

        return CreatureDisplayService.CalculateAbilityBonus(abilityScore);
    }

    public override void ClearPanel()
    {
        _skills.Clear();
        _allSkills.Clear();
        _classSkillIds.Clear();
        _unavailableSkillIds.Clear();
        CurrentCreature = null;
        SetText(_skillsSummaryText, "0 skills with ranks");
        if (_noSkillsText != null)
            _noSkillsText.IsVisible = true;
    }

    private void ApplySortAndFilter()
    {
        if (_allSkills.Count == 0)
            return;

        var filtered = _allSkills.AsEnumerable();

        bool trainedOnly = _trainedOnlyCheckBox?.IsChecked ?? false;
        if (trainedOnly)
            filtered = filtered.Where(s => s.Ranks > 0);

        int sortIndex = _sortComboBox?.SelectedIndex ?? 0;
        filtered = sortIndex switch
        {
            0 => filtered.OrderBy(s => s.SkillName),
            1 => filtered.OrderByDescending(s => s.Ranks).ThenBy(s => s.SkillName),
            2 => filtered.OrderByDescending(s => s.IsClassSkill).ThenBy(s => s.SkillName),
            _ => filtered.OrderBy(s => s.SkillName)
        };

        _skills.Clear();
        foreach (var skill in filtered)
            _skills.Add(skill);

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

        return skillId switch
        {
            0 => "CHA",
            1 => "CON",
            2 => "INT",
            3 => "STR",
            4 => "WIS",
            5 => "DEX",
            6 => "WIS",
            7 => "INT",
            8 => "DEX",
            9 => "DEX",
            10 => "DEX",
            11 => "CHA",
            12 => "CHA",
            13 => "DEX",
            14 => "INT",
            15 => "DEX",
            16 => "INT",
            17 => "WIS",
            18 => "CHA",
            19 => "CHA",
            20 => "INT",
            21 => "DEX",
            22 => "INT",
            23 => "CHA",
            24 => "CHA",
            25 => "INT",
            26 => "INT",
            27 => "DEX",
            _ => "INT"
        };
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

    public void SetIconService(ItemIconService? iconService)
    {
        _iconService = iconService;
    }

    public Bitmap? IconBitmap
    {
        get
        {
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

    public bool HasGameIcon => _iconService != null && _iconService.IsGameDataAvailable;
}
