using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Utc;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class FeatsPanel : UserControl
{
    private CreatureDisplayService? _displayService;
    private TextBlock? _featsSummaryText;
    private ItemsControl? _featsList;
    private TextBlock? _noFeatsText;
    private Border? _specialAbilitiesSection;
    private ItemsControl? _specialAbilitiesList;
    private TextBlock? _noAbilitiesText;

    private ObservableCollection<FeatViewModel> _feats = new();
    private ObservableCollection<SpecialAbilityViewModel> _abilities = new();

    public FeatsPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _featsSummaryText = this.FindControl<TextBlock>("FeatsSummaryText");
        _featsList = this.FindControl<ItemsControl>("FeatsList");
        _noFeatsText = this.FindControl<TextBlock>("NoFeatsText");
        _specialAbilitiesSection = this.FindControl<Border>("SpecialAbilitiesSection");
        _specialAbilitiesList = this.FindControl<ItemsControl>("SpecialAbilitiesList");
        _noAbilitiesText = this.FindControl<TextBlock>("NoAbilitiesText");

        if (_featsList != null)
            _featsList.ItemsSource = _feats;
        if (_specialAbilitiesList != null)
            _specialAbilitiesList.ItemsSource = _abilities;
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
        _feats.Clear();
        _abilities.Clear();

        if (creature == null)
        {
            ClearPanel();
            return;
        }

        // Load feats - use display service for name resolution
        foreach (var featId in creature.FeatList.OrderBy(f => GetFeatNameInternal(f)))
        {
            _feats.Add(new FeatViewModel
            {
                FeatId = featId,
                FeatName = GetFeatNameInternal(featId),
                FeatDescription = $"Feat ID: {featId}"
            });
        }

        SetText(_featsSummaryText, $"{_feats.Count} feats");
        if (_noFeatsText != null)
            _noFeatsText.IsVisible = _feats.Count == 0;

        // Load special abilities - use display service for spell name resolution
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
    }

    public void ClearPanel()
    {
        _feats.Clear();
        _abilities.Clear();
        SetText(_featsSummaryText, "0 feats");
        if (_noFeatsText != null)
            _noFeatsText.IsVisible = true;
        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = true;
    }

    private string GetFeatNameInternal(ushort featId)
    {
        // Use display service for 2DA/TLK lookup
        if (_displayService != null)
            return _displayService.GetFeatName(featId);

        // Fallback if no display service
        return $"Feat {featId}";
    }

    private string GetSpellNameInternal(ushort spellId)
    {
        // Use display service for 2DA/TLK lookup
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

public class FeatViewModel
{
    public ushort FeatId { get; set; }
    public string FeatName { get; set; } = "";
    public string FeatDescription { get; set; } = "";
}

public class SpecialAbilityViewModel
{
    public ushort SpellId { get; set; }
    public string AbilityName { get; set; } = "";
    public byte CasterLevel { get; set; }
    public string CasterLevelDisplay { get; set; } = "";
    public byte Flags { get; set; }
}
