using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using Radoub.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
    private CheckBox? _hideUnavailableCheckBox;
    private Border? _skillPointsTableBorder;
    private StackPanel? _skillPointsTablePanel;

    private ObservableCollection<SkillViewModel> _skills = new();
    private List<SkillViewModel> _allSkills = new();
    private HashSet<int> _classSkillIds = new();
    private HashSet<int> _unavailableSkillIds = new();
    private int _totalLevel;

    public SkillsPanel()
    {
        InitializeComponent();

        // Subscribe to theme changes to refresh color-dependent bindings
        SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.CurrentThemeId) ||
            e.PropertyName == nameof(SettingsService.FontFamily))
        {
            // Theme or font changed - notify all view models to refresh bindings
            foreach (var skill in _allSkills)
            {
                skill.NotifyColorChanged();
            }
            // Also refresh the skill points table which uses theme colors
            UpdateSkillPointsTable();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _skillsSummaryText = this.FindControl<TextBlock>("SkillsSummaryText");
        _skillsList = this.FindControl<ItemsControl>("SkillsList");
        _noSkillsText = this.FindControl<TextBlock>("NoSkillsText");
        _sortComboBox = this.FindControl<ComboBox>("SortComboBox");
        _trainedOnlyCheckBox = this.FindControl<CheckBox>("TrainedOnlyCheckBox");
        _hideUnavailableCheckBox = this.FindControl<CheckBox>("HideUnavailableCheckBox");
        _skillPointsTableBorder = this.FindControl<Border>("SkillPointsTableBorder");
        _skillPointsTablePanel = this.FindControl<StackPanel>("SkillPointsTablePanel");

        if (_skillsList != null)
            _skillsList.ItemsSource = _skills;

        if (_sortComboBox != null)
            _sortComboBox.SelectionChanged += (s, e) => ApplySortAndFilter();

        if (_trainedOnlyCheckBox != null)
            _trainedOnlyCheckBox.IsCheckedChanged += (s, e) => ApplySortAndFilter();

        if (_hideUnavailableCheckBox != null)
            _hideUnavailableCheckBox.IsCheckedChanged += (s, e) => ApplySortAndFilter();
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
        if (_skillPointsTableBorder != null)
            _skillPointsTableBorder.IsVisible = false;
        if (_skillPointsTablePanel != null)
            _skillPointsTablePanel.Children.Clear();
    }

    private void ApplySortAndFilter()
    {
        if (_allSkills.Count == 0)
            return;

        var filtered = _allSkills.AsEnumerable();

        // Filter: Trained Only
        bool trainedOnly = _trainedOnlyCheckBox?.IsChecked ?? false;
        if (trainedOnly)
            filtered = filtered.Where(s => s.Ranks > 0);

        // Filter: Hide Unavailable
        bool hideUnavailable = _hideUnavailableCheckBox?.IsChecked ?? false;
        if (hideUnavailable)
            filtered = filtered.Where(s => !s.IsUnavailable);

        // Sort
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

        // Update skill points table
        UpdateSkillPointsTable();
    }

    /// <summary>
    /// Updates the skill points summary table showing points per class.
    /// </summary>
    private void UpdateSkillPointsTable()
    {
        if (_skillPointsTablePanel == null || _skillPointsTableBorder == null) return;

        _skillPointsTablePanel.Children.Clear();

        if (CurrentCreature == null || CurrentCreature.ClassList.Count == 0)
        {
            _skillPointsTableBorder.IsVisible = false;
            return;
        }

        _skillPointsTableBorder.IsVisible = true;

        var totalSkillPoints = 0;
        var totalRanksSpent = _allSkills.Sum(s => s.Ranks);

        foreach (var classEntry in CurrentCreature.ClassList)
        {
            var className = _displayService?.GetClassName(classEntry.Class) ?? $"Class {classEntry.Class}";
            var skillPointBase = GetClassSkillPointBase(classEntry.Class);
            var intModifier = CreatureDisplayService.CalculateAbilityBonus(CurrentCreature.Int);

            // Skill points per level = base + INT modifier (minimum 1)
            var pointsPerLevel = Math.Max(1, skillPointBase + intModifier);
            // First level gets 4x points
            var firstLevelPoints = pointsPerLevel * 4;
            var additionalLevelPoints = pointsPerLevel * (classEntry.ClassLevel - 1);
            var classPoints = firstLevelPoints + additionalLevelPoints;

            // For multiclass, only first class gets 4x at level 1
            // This is an approximation since we don't know level-up order
            if (CurrentCreature.ClassList.IndexOf(classEntry) > 0)
            {
                classPoints = pointsPerLevel * classEntry.ClassLevel;
            }

            totalSkillPoints += classPoints;

            // Create row for this class
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Avalonia.Thickness(0, 2) };

            // Get theme font sizes
            var normalFontSize = this.FindResource("FontSizeNormal") as double? ?? 14;
            var smallFontSize = this.FindResource("FontSizeSmall") as double? ?? 12;
            var xsmallFontSize = this.FindResource("FontSizeXSmall") as double? ?? 10;

            var classLabel = new TextBlock
            {
                Text = $"{className} ({classEntry.ClassLevel}):",
                FontSize = smallFontSize,
                MinWidth = 100,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            row.Children.Add(classLabel);

            var pointsLabel = new TextBlock
            {
                Text = $"{skillPointBase}+INT = {pointsPerLevel}/lvl",
                FontSize = xsmallFontSize,
                Foreground = this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush ?? GetDisabledBrush(),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            row.Children.Add(pointsLabel);

            _skillPointsTablePanel.Children.Add(row);
        }

        // Get theme font sizes for remaining elements
        var normalSize = this.FindResource("FontSizeNormal") as double? ?? 14;
        var smallSize = this.FindResource("FontSizeSmall") as double? ?? 12;
        var xsmallSize = this.FindResource("FontSizeXSmall") as double? ?? 10;

        // Add separator
        var separator = new Border
        {
            Height = 1,
            Background = this.FindResource("SystemControlForegroundBaseLowBrush") as IBrush ?? GetDisabledBrush(),
            Margin = new Avalonia.Thickness(0, 6)
        };
        _skillPointsTablePanel.Children.Add(separator);

        // Add total summary
        var usageColor = totalRanksSpent > totalSkillPoints ? GetErrorBrush() :
                         totalRanksSpent == totalSkillPoints ? GetSuccessBrush() :
                         this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? Brushes.White;

        var totalRow = new TextBlock
        {
            Text = $"Total: {totalRanksSpent} / ~{totalSkillPoints} points",
            FontSize = smallSize,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = usageColor
        };
        _skillPointsTablePanel.Children.Add(totalRow);

        // Add note about approximation
        var noteRow = new TextBlock
        {
            Text = "(Estimate - excludes race/feat bonuses)",
            FontSize = xsmallSize,
            FontStyle = Avalonia.Media.FontStyle.Italic,
            Foreground = this.FindResource("SystemControlForegroundBaseMediumLowBrush") as IBrush ?? GetDisabledBrush(),
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        _skillPointsTablePanel.Children.Add(noteRow);
    }

    /// <summary>
    /// Gets the base skill points per level for a class from 2DA via service.
    /// </summary>
    private int GetClassSkillPointBase(int classId)
    {
        return _displayService?.GetClassSkillPointBase(classId) ?? 2;
    }

    private IBrush GetSuccessBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeSuccess", ThemeVariant.Default, out var brush) == true && brush is IBrush b)
            return b;
        return new SolidColorBrush(Color.Parse("#388E3C"));
    }

    private IBrush GetErrorBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeError", ThemeVariant.Default, out var brush) == true && brush is IBrush b)
            return b;
        return new SolidColorBrush(Color.Parse("#D32F2F"));
    }

    private string GetSkillName(int skillId)
    {
        return _displayService?.GetSkillName(skillId) ?? $"Skill {skillId}";
    }

    private string GetSkillKeyAbility(int skillId)
    {
        return _displayService?.GetSkillKeyAbility(skillId) ?? "INT";
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
    /// Display string for ability modifier (+X, -X, or +0).
    /// </summary>
    public string ModifierDisplay => AbilityModifier >= 0 ? $"+{AbilityModifier}" : AbilityModifier.ToString();

    /// <summary>
    /// Color for the modifier display (green for positive, red for negative, gray for zero).
    /// Uses theme colors when available.
    /// </summary>
    public IBrush ModifierColor
    {
        get
        {
            var app = Application.Current;
            if (AbilityModifier > 0)
            {
                if (app?.Resources.TryGetResource("ThemeSuccess", ThemeVariant.Default, out var brush) == true && brush is IBrush b)
                    return b;
                return new SolidColorBrush(Color.Parse("#388E3C")); // Green fallback
            }
            if (AbilityModifier < 0)
            {
                if (app?.Resources.TryGetResource("ThemeError", ThemeVariant.Default, out var brush) == true && brush is IBrush b)
                    return b;
                return new SolidColorBrush(Color.Parse("#D32F2F")); // Red fallback
            }
            if (app?.Resources.TryGetResource("ThemeDisabled", ThemeVariant.Default, out var grayBrush) == true && grayBrush is IBrush g)
                return g;
            return new SolidColorBrush(Color.Parse("#757575")); // Gray fallback
        }
    }

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
                catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or ArgumentException)
                {
                    // Icon not available - expected for some skills
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not load skill icon for ID {SkillId}: {ex.Message}");
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

    /// <summary>
    /// Called when the theme changes to notify bindings that color properties need refresh.
    /// </summary>
    public void NotifyColorChanged()
    {
        OnPropertyChanged(nameof(ModifierColor));
        OnPropertyChanged(nameof(RowBackground));
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
