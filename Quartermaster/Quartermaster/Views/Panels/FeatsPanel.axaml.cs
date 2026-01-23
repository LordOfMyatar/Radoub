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

    /// <summary>
    /// Raised when the creature's special abilities are modified.
    /// </summary>
    public event EventHandler? SpecialAbilitiesChanged;

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

    private void LoadSpecialAbilities(UtcFile creature)
    {
        foreach (var ability in creature.SpecAbilityList)
        {
            var vm = new SpecialAbilityViewModel
            {
                SpellId = ability.Spell,
                AbilityName = GetSpellNameInternal(ability.Spell),
                CasterLevelDisplay = $"CL {ability.SpellCasterLevel}",
                Flags = ability.SpellFlags
            };
            // Set CasterLevel last to avoid triggering callback during load
            vm._casterLevel = ability.SpellCasterLevel;
            vm.OnCasterLevelChanged = OnAbilityCasterLevelChanged;
            vm.OnFlagsChanged = OnAbilityFlagsChanged;
            vm.RemoveCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => RemoveAbility(vm));
            _abilities.Add(vm);
        }

        UpdateAbilitiesVisibility();
    }

    private void UpdateAbilitiesVisibility()
    {
        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = _abilities.Count == 0;

        // Show expander if there are abilities
        if (_specialAbilitiesExpander != null && _abilities.Count > 0)
            _specialAbilitiesExpander.IsExpanded = true;
    }

    private void OnAbilityCasterLevelChanged(SpecialAbilityViewModel vm)
    {
        if (_isLoading || _currentCreature == null) return;

        // Update the creature's SpecAbilityList
        var ability = _currentCreature.SpecAbilityList.FirstOrDefault(a => a.Spell == vm.SpellId);
        if (ability != null)
        {
            ability.SpellCasterLevel = vm.CasterLevel;
            SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnAbilityFlagsChanged(SpecialAbilityViewModel vm)
    {
        if (_isLoading || _currentCreature == null) return;

        // Update the creature's SpecAbilityList
        var ability = _currentCreature.SpecAbilityList.FirstOrDefault(a => a.Spell == vm.SpellId);
        if (ability != null)
        {
            ability.SpellFlags = vm.Flags;
            SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RemoveAbility(SpecialAbilityViewModel vm)
    {
        if (_currentCreature == null) return;

        // Remove from creature
        var ability = _currentCreature.SpecAbilityList.FirstOrDefault(a => a.Spell == vm.SpellId);
        if (ability != null)
        {
            _currentCreature.SpecAbilityList.Remove(ability);
        }

        // Remove from UI
        _abilities.Remove(vm);
        UpdateAbilitiesVisibility();

        SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnAddAbilityClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_displayService == null || _currentCreature == null) return;

        // Get all spells for picker
        var spellIds = _displayService.GetAllSpellIds();
        var spells = new List<(int Id, string Name, int InnateLevel)>();

        foreach (var spellId in spellIds)
        {
            var spellName = _displayService.GetSpellName(spellId);
            var spellInfo = _displayService.GetSpellInfo(spellId);
            int innateLevel = spellInfo?.InnateLevel ?? 0;
            spells.Add((spellId, spellName, innateLevel));
        }

        var picker = new Dialogs.SpellPickerWindow(spells);
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow != null)
        {
            await picker.ShowDialog(parentWindow);
        }
        else
        {
            picker.Show();
        }

        if (picker.Confirmed && picker.SelectedSpellId.HasValue)
        {
            var spellId = picker.SelectedSpellId.Value;

            // Check if already exists
            if (_currentCreature.SpecAbilityList.Any(a => a.Spell == spellId))
            {
                return; // Already has this ability
            }

            // Add to creature
            var newAbility = new Radoub.Formats.Utc.SpecialAbility
            {
                Spell = spellId,
                SpellCasterLevel = 1, // Default caster level
                SpellFlags = 0x01 // Default: readied
            };
            _currentCreature.SpecAbilityList.Add(newAbility);

            // Add to UI
            var vm = new SpecialAbilityViewModel
            {
                SpellId = spellId,
                AbilityName = picker.SelectedSpellName,
                CasterLevelDisplay = "CL 1",
                Flags = 0x01
            };
            vm._casterLevel = 1;
            vm.OnCasterLevelChanged = OnAbilityCasterLevelChanged;
            vm.OnFlagsChanged = OnAbilityFlagsChanged;
            vm.RemoveCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => RemoveAbility(vm));
            _abilities.Add(vm);

            UpdateAbilitiesVisibility();
            SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
        }
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

        // Apply category filter (ComboBox)
        int categoryIndex = _categoryFilterComboBox?.SelectedIndex ?? 0;
        filtered = categoryIndex switch
        {
            0 => filtered, // All Categories
            1 => filtered.Where(f => f.Category == FeatCategory.Combat),
            2 => filtered.Where(f => f.Category == FeatCategory.ActiveCombat),
            3 => filtered.Where(f => f.Category == FeatCategory.Defensive),
            4 => filtered.Where(f => f.Category == FeatCategory.Magical),
            5 => filtered.Where(f => f.Category == FeatCategory.ClassRacial),
            6 => filtered.Where(f => f.Category == FeatCategory.Other),
            _ => filtered
        };

        // Apply status filter (checkboxes - OR logic for enabled statuses)
        // Filter should match the StatusText shown to users exactly
        var showAssigned = _showAssignedCheckBox?.IsChecked ?? true;
        var showGranted = _showGrantedCheckBox?.IsChecked ?? true;
        var showAvailable = _showAvailableCheckBox?.IsChecked ?? true;
        var showPrereqsUnmet = _showPrereqsUnmetCheckBox?.IsChecked ?? true;
        var showUnavailable = _showUnavailableCheckBox?.IsChecked ?? false;

        // Filter by status - match the displayed StatusText exactly
        filtered = filtered.Where(f =>
        {
            return f.StatusText switch
            {
                "Granted" => showGranted,
                "Assigned" => showAssigned,
                "Unavailable" => showUnavailable,
                "Available" => showAvailable,
                "Prereqs Unmet" => showPrereqsUnmet,
                _ => false // Unknown status - don't show
            };
        });

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

        // Calculate expected choosable feats (not including granted)
        var selectedCount = assignedCount - grantedCount; // Manually chosen feats
        string expectedNote = "";
        if (_currentCreature != null && _displayService != null)
        {
            var expectedInfo = _displayService.Feats.GetExpectedFeatCount(_currentCreature);
            int expected = expectedInfo.TotalExpected;
            int diff = selectedCount - expected;

            if (diff > 0)
                expectedNote = $" | Chosen: {selectedCount}/{expected} (+{diff})";
            else if (diff < 0)
                expectedNote = $" | Chosen: {selectedCount}/{expected} ({diff} available)";
            else
                expectedNote = $" | Chosen: {selectedCount}/{expected}";
        }

        SetText(_featsSummaryText,
            $"{assignedCount} assigned ({grantedCount} granted){expectedNote} | {unavailableCount} unavailable{filterNote}");

        // Update the left-side assigned feats list
        UpdateAssignedFeatsList();
    }

    /// <summary>
    /// Updates the assigned feats list panel on the left side.
    /// Shows feat names grouped by race, class (for granted feats), and a "Selected" section for manually assigned.
    /// </summary>
    private void UpdateAssignedFeatsList()
    {
        if (_assignedFeatsListPanel == null || _assignedFeatsListBorder == null) return;

        _assignedFeatsListPanel.Children.Clear();

        if (_currentCreature == null || _displayService == null || _assignedFeatIds.Count == 0)
        {
            _assignedFeatsListBorder.IsVisible = false;
            return;
        }

        // Get theme-aware font sizes
        var smallFontSize = this.FindResource("FontSizeSmall") as double? ?? 12;
        var xsmallFontSize = this.FindResource("FontSizeXSmall") as double? ?? 10;

        // Group feats by their source
        var racialFeats = new List<ushort>();
        var grantedByClass = new Dictionary<int, List<ushort>>(); // classId -> feat IDs
        var selectedFeats = new List<ushort>(); // manually selected (not granted by any class/race)

        foreach (var featId in _assignedFeatIds.OrderBy(f => GetFeatNameInternal(f)))
        {
            if (_grantedFeatIds.Contains(featId))
            {
                // Check if it's a racial feat first
                if (_displayService.IsFeatGrantedByRace(_currentCreature, featId))
                {
                    racialFeats.Add(featId);
                }
                else
                {
                    // Find which class grants this feat
                    var grantingClassId = _displayService.GetFeatGrantingClass(_currentCreature, featId);
                    if (grantingClassId >= 0)
                    {
                        if (!grantedByClass.ContainsKey(grantingClassId))
                            grantedByClass[grantingClassId] = new List<ushort>();
                        grantedByClass[grantingClassId].Add(featId);
                    }
                    else
                    {
                        // Granted but couldn't determine source - put in selected
                        selectedFeats.Add(featId);
                    }
                }
            }
            else
            {
                selectedFeats.Add(featId);
            }
        }

        bool hasAnyFeats = false;

        // Show racial feats first
        if (racialFeats.Count > 0)
        {
            hasAnyFeats = true;
            var raceName = _displayService.GetRaceName(_currentCreature.Race);

            // Race header
            var raceHeader = new TextBlock
            {
                Text = raceName,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = smallFontSize,
                Foreground = GetInfoBrush(),
                Margin = new Avalonia.Thickness(0, 0, 0, 6)
            };
            _assignedFeatsListPanel.Children.Add(raceHeader);

            // Racial feat names
            foreach (var featId in racialFeats)
            {
                var featName = GetFeatNameInternal(featId);
                var featText = new TextBlock
                {
                    Text = $"  {featName}",
                    FontSize = xsmallFontSize,
                    Foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? GetDisabledBrush(),
                    Margin = new Avalonia.Thickness(0, 2, 0, 0)
                };
                _assignedFeatsListPanel.Children.Add(featText);
            }
        }

        // Show class-granted feats
        foreach (var classEntry in _currentCreature.ClassList)
        {
            var classId = (int)classEntry.Class;
            if (!grantedByClass.ContainsKey(classId)) continue;

            var className = _displayService.GetClassName(classEntry.Class) ?? $"Class {classEntry.Class}";
            var featsForClass = grantedByClass[classId];

            hasAnyFeats = true;

            // Class header
            var classHeader = new TextBlock
            {
                Text = className,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = smallFontSize,
                Foreground = GetSelectionBrush(),
                Margin = new Avalonia.Thickness(0, _assignedFeatsListPanel.Children.Count > 0 ? 12 : 0, 0, 6)
            };
            _assignedFeatsListPanel.Children.Add(classHeader);

            // Feat names
            foreach (var featId in featsForClass)
            {
                var featName = GetFeatNameInternal(featId);
                var featText = new TextBlock
                {
                    Text = $"  {featName}",
                    FontSize = xsmallFontSize,
                    Foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? GetDisabledBrush(),
                    Margin = new Avalonia.Thickness(0, 2, 0, 0)
                };
                _assignedFeatsListPanel.Children.Add(featText);
            }
        }

        // Show manually assigned feats (not granted by class/race)
        if (selectedFeats.Count > 0)
        {
            hasAnyFeats = true;

            // Assigned header (matches status column terminology)
            var selectedHeader = new TextBlock
            {
                Text = "Assigned",
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = smallFontSize,
                Foreground = GetSuccessBrush(),
                Margin = new Avalonia.Thickness(0, _assignedFeatsListPanel.Children.Count > 0 ? 12 : 0, 0, 6)
            };
            _assignedFeatsListPanel.Children.Add(selectedHeader);

            // Feat names
            foreach (var featId in selectedFeats)
            {
                var featName = GetFeatNameInternal(featId);
                var featText = new TextBlock
                {
                    Text = $"  {featName}",
                    FontSize = xsmallFontSize,
                    Foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? GetDisabledBrush(),
                    Margin = new Avalonia.Thickness(0, 2, 0, 0)
                };
                _assignedFeatsListPanel.Children.Add(featText);
            }
        }

        _assignedFeatsListBorder.IsVisible = hasAnyFeats;
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
        // Reset status checkboxes to defaults
        if (_showAssignedCheckBox != null)
            _showAssignedCheckBox.IsChecked = true;
        if (_showGrantedCheckBox != null)
            _showGrantedCheckBox.IsChecked = true;
        if (_showAvailableCheckBox != null)
            _showAvailableCheckBox.IsChecked = true;
        if (_showPrereqsUnmetCheckBox != null)
            _showPrereqsUnmetCheckBox.IsChecked = true;
        if (_showUnavailableCheckBox != null)
            _showUnavailableCheckBox.IsChecked = false;
        // Hide the assigned feats list panel
        if (_assignedFeatsListBorder != null)
            _assignedFeatsListBorder.IsVisible = false;
        if (_assignedFeatsListPanel != null)
            _assignedFeatsListPanel.Children.Clear();
    }

    private void OnFeatAssignedChanged(FeatListViewModel feat, bool isNowAssigned)
    {
        if (_isLoading || _currentCreature == null) return;

        if (isNowAssigned)
        {
            AddFeat(feat.FeatId);
        }
        else
        {
            RemoveFeat(feat.FeatId);
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

    // Fallback brush for when theme resource is not available
    private static readonly IBrush FallbackBrush = Brushes.Gray;

    private IBrush GetDisabledBrush() =>
        this.FindResource("ThemeDisabled") as IBrush
        ?? this.FindResource("SystemControlForegroundBaseMediumLowBrush") as IBrush
        ?? FallbackBrush;

    private IBrush GetSuccessBrush() =>
        this.FindResource("ThemeSuccess") as IBrush
        ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush
        ?? FallbackBrush;

    private IBrush GetWarningBrush() =>
        this.FindResource("ThemeWarning") as IBrush
        ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush
        ?? FallbackBrush;

    private IBrush GetInfoBrush() =>
        this.FindResource("ThemeInfo") as IBrush
        ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush
        ?? FallbackBrush;

    private IBrush GetSelectionBrush() =>
        this.FindResource("ThemeSelection") as IBrush
        ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush
        ?? FallbackBrush;

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

public class FeatListViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private Bitmap? _iconBitmap;
    private bool _iconLoaded = false;
    private ItemIconService? _iconService;
    private bool _isAssigned;
    private string _statusText = "";
    private IBrush _statusColor = Brushes.Transparent;
    private IBrush _rowBackground = Brushes.Transparent;
    private double _textOpacity = 1.0;

    public ushort FeatId { get; set; }
    public string FeatName { get; set; } = "";
    public string Description { get; set; } = "";
    public FeatCategory Category { get; set; }
    public string CategoryName { get; set; } = "";

    public bool IsAssigned
    {
        get => _isAssigned;
        set
        {
            if (_isAssigned != value)
            {
                _isAssigned = value;
                OnPropertyChanged(nameof(IsAssigned));
                OnPropertyChanged(nameof(CanToggle));
                OnPropertyChanged(nameof(AssignedTooltip));
                OnAssignedChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsGranted { get; set; }
    public bool IsUnavailable { get; set; }
    public bool HasPrerequisites { get; set; }
    public bool PrerequisitesMet { get; set; }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public IBrush StatusColor
    {
        get => _statusColor;
        set
        {
            if (_statusColor != value)
            {
                _statusColor = value;
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

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
            if (Math.Abs(_textOpacity - value) > 0.001)
            {
                _textOpacity = value;
                OnPropertyChanged(nameof(TextOpacity));
            }
        }
    }

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
            OnPropertyChanged(nameof(IconBitmap));
            OnPropertyChanged(nameof(HasGameIcon));
        }
    }

    /// <summary>
    /// Whether we have a real game icon (not placeholder).
    /// Returns true if icon service is available (assumes icon exists to avoid triggering load).
    /// </summary>
    public bool HasGameIcon => _iconService != null && _iconService.IsGameDataAvailable;

    /// <summary>
    /// Can the checkbox be toggled? (Not a granted feat - can't remove class-granted feats)
    /// </summary>
    public bool CanToggle => !IsGranted;

    /// <summary>
    /// Tooltip for the assigned checkbox.
    /// </summary>
    public string AssignedTooltip
    {
        get
        {
            if (IsGranted) return "Granted by class - cannot remove";
            if (IsUnavailable && !IsAssigned) return "Not available to this class/race";
            return IsAssigned ? "Click to remove feat" : "Click to add feat";
        }
    }

    /// <summary>
    /// Callback when IsAssigned changes. Args: (FeatListViewModel feat, bool newValue)
    /// </summary>
    public Action<FeatListViewModel, bool>? OnAssignedChanged { get; set; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

public class SpecialAbilityViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public ushort SpellId { get; set; }
    public string AbilityName { get; set; } = "";

    internal byte _casterLevel;
    public byte CasterLevel
    {
        get => _casterLevel;
        set
        {
            if (SetProperty(ref _casterLevel, value))
            {
                CasterLevelDisplay = $"CL {value}";
                OnCasterLevelChanged?.Invoke(this);
            }
        }
    }

    public string CasterLevelDisplay { get; set; } = "";

    private byte _flags;
    public byte Flags
    {
        get => _flags;
        set => SetProperty(ref _flags, value);
    }

    // Flag 0x04 = unlimited uses
    public bool IsUnlimited
    {
        get => (Flags & 0x04) != 0;
        set
        {
            if (value)
                Flags = (byte)(Flags | 0x04);
            else
                Flags = (byte)(Flags & ~0x04);
            OnPropertyChanged();
            OnFlagsChanged?.Invoke(this);
        }
    }

    public Action<SpecialAbilityViewModel>? OnCasterLevelChanged { get; set; }
    public Action<SpecialAbilityViewModel>? OnFlagsChanged { get; set; }
    public System.Windows.Input.ICommand? RemoveCommand { get; set; }
}
