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

public partial class SkillsPanel : BasePanelControl
{
    private CreatureDisplayService? _displayService;
    private ItemIconService? _itemIconService;
    private bool _isLoading;

    /// <summary>
    /// Raised when the user modifies skill ranks.
    /// </summary>
    public event EventHandler? SkillsChanged;

    private TextBlock? _skillsSummaryText;
    private ItemsControl? _skillsList;
    private TextBlock? _noSkillsText;
    private ComboBox? _sortComboBox;
    private CheckBox? _trainedOnlyCheckBox;

    private ObservableCollection<SkillViewModel> _skills = new();
    private List<SkillViewModel> _allSkills = new();
    private HashSet<int> _classSkillIds = new();
    private HashSet<int> _unavailableSkillIds = new();
    private int _totalLevel;

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

    private void OnIncrementClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SkillViewModel skill)
        {
            if (skill.CanIncrement)
            {
                skill.Ranks++;
            }
        }
    }

    private void OnDecrementClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SkillViewModel skill)
        {
            if (skill.CanDecrement)
            {
                skill.Ranks--;
            }
        }
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
        _isLoading = true;

        _skills.Clear();
        _allSkills.Clear();
        _classSkillIds.Clear();
        _unavailableSkillIds.Clear();
        CurrentCreature = creature;

        if (creature == null || creature.SkillList.Count == 0)
        {
            ClearPanel();
            _isLoading = false;
            return;
        }

        // Calculate total character level for max rank calculation
        _totalLevel = creature.ClassList.Sum(c => c.ClassLevel);

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

            // Calculate max ranks: class skill = level + 3, cross-class = (level + 3) / 2
            var maxRanks = isClassSkill ? _totalLevel + 3 : (_totalLevel + 3) / 2;

            IBrush rowBackground;
            double textOpacity;
            if (isUnavailable)
            {
                rowBackground = GetTransparentRowBackground(GetDisabledBrush(), 20);
                textOpacity = 0.5;
            }
            else if (isClassSkill)
            {
                rowBackground = GetTransparentRowBackground(GetInfoBrush(), 30);
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
                AbilityModifier = abilityModifier,
                IsClassSkill = isClassSkill,
                IsUnavailable = isUnavailable,
                MaxRanks = maxRanks,
                ClassSkillIndicator = isUnavailable ? "✗" : (isClassSkill ? "●" : "○"),
                RowBackground = rowBackground,
                TextOpacity = textOpacity
            };

            // Set ranks after MaxRanks is set so CanIncrement/CanDecrement calculate correctly
            vm.Ranks = ranks;

            // Wire up change handler
            vm.OnRanksChanged = OnSkillRanksChanged;

            vm.SetIconService(_itemIconService);
            _allSkills.Add(vm);
        }

        ApplySortAndFilter();
        UpdateSummary();

        if (_noSkillsText != null)
            _noSkillsText.IsVisible = false;

        _isLoading = false;
    }

    private void OnSkillRanksChanged(SkillViewModel skill, int newRanks)
    {
        if (_isLoading || CurrentCreature == null) return;

        // Update the UtcFile's skill list
        if (skill.SkillId < CurrentCreature.SkillList.Count)
        {
            CurrentCreature.SkillList[skill.SkillId] = (byte)newRanks;
        }

        // Update summary
        UpdateSummary();

        // Notify that skills changed (for dirty tracking)
        SkillsChanged?.Invoke(this, EventArgs.Empty);
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
        _totalLevel = 0;
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

        // Include character level and max rank info in summary
        var maxClassSkillRank = _totalLevel + 3;
        var maxCrossClassRank = (_totalLevel + 3) / 2;

        SetText(_skillsSummaryText,
            $"Level {_totalLevel}: {skillsWithRanks} skills with ranks ({totalRanks} total) | Max: {maxClassSkillRank} class / {maxCrossClassRank} cross{filterNote}");
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

    #region Theme-Aware Colors

    // Light theme default colors for fallback
    private static readonly IBrush DefaultDisabledBrush = new SolidColorBrush(Color.Parse("#757575")); // Gray
    private static readonly IBrush DefaultInfoBrush = new SolidColorBrush(Color.Parse("#1976D2"));     // Blue

    private IBrush GetDisabledBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeDisabled", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultDisabledBrush;
    }

    private IBrush GetInfoBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeInfo", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultInfoBrush;
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

/// <summary>
/// View model for a skill in the skills list.
/// Supports rank editing via +/- buttons with max rank validation.
/// </summary>
public class SkillViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private Bitmap? _iconBitmap;
    private bool _iconLoaded = false;
    private ItemIconService? _iconService;
    private int _ranks;
    private int _total;
    private IBrush _rowBackground = Brushes.Transparent;
    private double _textOpacity = 1.0;

    public int SkillId { get; set; }
    public string SkillName { get; set; } = "";
    public string KeyAbility { get; set; } = "";
    public int AbilityModifier { get; set; }
    public bool IsClassSkill { get; set; }
    public bool IsUnavailable { get; set; }
    public string ClassSkillIndicator { get; set; } = "○";

    /// <summary>
    /// Maximum ranks allowed for this skill based on character level and class skill status.
    /// Class skill: level + 3, Cross-class: (level + 3) / 2
    /// </summary>
    public int MaxRanks { get; set; }

    /// <summary>
    /// Current skill ranks (modifiable).
    /// </summary>
    public int Ranks
    {
        get => _ranks;
        set
        {
            if (_ranks != value)
            {
                _ranks = value;
                OnPropertyChanged(nameof(Ranks));
                OnPropertyChanged(nameof(RanksDisplay));
                OnPropertyChanged(nameof(CanIncrement));
                OnPropertyChanged(nameof(CanDecrement));
                UpdateTotal();
                OnRanksChanged?.Invoke(this, value);
            }
        }
    }

    public string RanksDisplay => Ranks.ToString();

    public int Total
    {
        get => _total;
        private set
        {
            if (_total != value)
            {
                _total = value;
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalDisplay));
            }
        }
    }

    public string TotalDisplay => Total.ToString();

    public IBrush RowBackground
    {
        get => _rowBackground;
        set
        {
            if (_rowBackground != value)
            {
                _rowBackground = value;
                OnPropertyChanged(nameof(RowBackground));
            }
        }
    }

    public double TextOpacity
    {
        get => _textOpacity;
        set
        {
            if (System.Math.Abs(_textOpacity - value) > 0.001)
            {
                _textOpacity = value;
                OnPropertyChanged(nameof(TextOpacity));
            }
        }
    }

    /// <summary>
    /// Whether the + button should be enabled (ranks &lt; max and not unavailable).
    /// </summary>
    public bool CanIncrement => !IsUnavailable && Ranks < MaxRanks;

    /// <summary>
    /// Whether the - button should be enabled (ranks &gt; 0 and not unavailable).
    /// </summary>
    public bool CanDecrement => !IsUnavailable && Ranks > 0;

    /// <summary>
    /// Tooltip for the + button showing max rank info.
    /// </summary>
    public string IncrementTooltip => IsUnavailable
        ? "Skill unavailable to this character"
        : (Ranks >= MaxRanks ? $"At max ranks ({MaxRanks})" : $"Increase rank (max: {MaxRanks})");

    /// <summary>
    /// Tooltip for the - button.
    /// </summary>
    public string DecrementTooltip => IsUnavailable
        ? "Skill unavailable to this character"
        : (Ranks <= 0 ? "Already at minimum (0)" : "Decrease rank");

    /// <summary>
    /// Callback when ranks change. Args: (SkillViewModel skill, int newRanks)
    /// </summary>
    public Action<SkillViewModel, int>? OnRanksChanged { get; set; }

    private void UpdateTotal()
    {
        Total = Ranks + AbilityModifier;
    }

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
            OnPropertyChanged(nameof(IconBitmap));
            OnPropertyChanged(nameof(HasGameIcon));
        }
    }

    public bool HasGameIcon => _iconService != null && _iconService.IsGameDataAvailable;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
