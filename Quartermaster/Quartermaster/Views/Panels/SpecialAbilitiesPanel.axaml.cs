using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Quartermaster.ViewModels;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

/// <summary>
/// Dedicated panel for managing creature special abilities (spell-like abilities).
/// Extracted from FeatsPanel for better UX and discoverability.
/// </summary>
public partial class SpecialAbilitiesPanel : BasePanelControl
{
    private CreatureDisplayService? _displayService;

    private TextBlock? _abilitiesSummaryText;
    private ItemsControl? _abilitiesList;
    private TextBlock? _noAbilitiesText;
    private Grid? _columnHeaderGrid;
    private Button? _addAbilityButton;

    private readonly ObservableCollection<SpecialAbilityViewModel> _abilities = new();

    public event EventHandler? SpecialAbilitiesChanged;

    public SpecialAbilitiesPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _abilitiesSummaryText = this.FindControl<TextBlock>("AbilitiesSummaryText");
        _abilitiesList = this.FindControl<ItemsControl>("AbilitiesList");
        _noAbilitiesText = this.FindControl<TextBlock>("NoAbilitiesText");
        _columnHeaderGrid = this.FindControl<Grid>("ColumnHeaderGrid");
        _addAbilityButton = this.FindControl<Button>("AddAbilityButton");

        if (_abilitiesList != null)
            _abilitiesList.ItemsSource = _abilities;

        if (_addAbilityButton != null)
            _addAbilityButton.Click += OnAddAbilityClick;
    }

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
    }

    public override void LoadCreature(UtcFile? creature)
    {
        IsLoading = true;
        _abilities.Clear();
        CurrentCreature = creature;

        if (creature == null)
        {
            ClearPanel();
            IsLoading = false;
            return;
        }

        foreach (var ability in creature.SpecAbilityList)
        {
            var vm = new SpecialAbilityViewModel
            {
                SpellId = ability.Spell,
                AbilityName = GetSpellName(ability.Spell),
                CasterLevelDisplay = $"CL {ability.SpellCasterLevel}",
                Flags = ability.SpellFlags
            };
            // Set CasterLevel directly to avoid triggering callback during load
            vm._casterLevel = ability.SpellCasterLevel;
            vm.OnCasterLevelChanged = OnAbilityCasterLevelChanged;
            vm.OnFlagsChanged = OnAbilityFlagsChanged;
            vm.RemoveCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => RemoveAbility(vm));
            _abilities.Add(vm);
        }

        UpdateSummary();
        DeferLoadingReset();
    }

    public override void ClearPanel()
    {
        IsLoading = true;
        _abilities.Clear();
        CurrentCreature = null;
        SetText(_abilitiesSummaryText, "0 abilities assigned");

        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = true;
        if (_columnHeaderGrid != null)
            _columnHeaderGrid.IsVisible = false;

        IsLoading = false;
    }

    private void UpdateSummary()
    {
        var count = _abilities.Count;
        SetText(_abilitiesSummaryText, $"{count} {(count == 1 ? "ability" : "abilities")} assigned");

        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = count == 0;
        if (_columnHeaderGrid != null)
            _columnHeaderGrid.IsVisible = count > 0;
    }

    private void OnAbilityCasterLevelChanged(SpecialAbilityViewModel vm)
    {
        if (IsLoading || CurrentCreature == null) return;

        var ability = CurrentCreature.SpecAbilityList.FirstOrDefault(a => a.Spell == vm.SpellId);
        if (ability != null)
        {
            ability.SpellCasterLevel = vm.CasterLevel;
            SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnAbilityFlagsChanged(SpecialAbilityViewModel vm)
    {
        if (IsLoading || CurrentCreature == null) return;

        var ability = CurrentCreature.SpecAbilityList.FirstOrDefault(a => a.Spell == vm.SpellId);
        if (ability != null)
        {
            ability.SpellFlags = vm.Flags;
            SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RemoveAbility(SpecialAbilityViewModel vm)
    {
        if (CurrentCreature == null) return;

        var ability = CurrentCreature.SpecAbilityList.FirstOrDefault(a => a.Spell == vm.SpellId);
        if (ability != null)
        {
            CurrentCreature.SpecAbilityList.Remove(ability);
        }

        _abilities.Remove(vm);
        UpdateSummary();
        SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnAddAbilityClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_displayService == null || CurrentCreature == null) return;

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

            // Check for duplicate
            if (CurrentCreature.SpecAbilityList.Any(a => a.Spell == spellId))
                return;

            var newAbility = new SpecialAbility
            {
                Spell = spellId,
                SpellCasterLevel = 1,
                SpellFlags = 0x01 // Readied
            };
            CurrentCreature.SpecAbilityList.Add(newAbility);

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

            UpdateSummary();
            SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private string GetSpellName(ushort spellId)
    {
        if (_displayService != null)
            return _displayService.GetSpellName(spellId);
        return $"Spell #{spellId}";
    }
}
