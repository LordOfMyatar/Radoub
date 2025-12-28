using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Utc;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class SkillsPanel : UserControl
{
    private TextBlock? _skillsSummaryText;
    private ItemsControl? _skillsList;
    private TextBlock? _noSkillsText;

    private ObservableCollection<SkillViewModel> _skills = new();

    // Standard NWN skill names (indexes into skills.2da)
    private static readonly string[] SkillNames = new[]
    {
        "Animal Empathy",    // 0
        "Concentration",     // 1
        "Disable Trap",      // 2
        "Discipline",        // 3
        "Heal",              // 4
        "Hide",              // 5
        "Listen",            // 6
        "Lore",              // 7
        "Move Silently",     // 8
        "Open Lock",         // 9
        "Parry",             // 10
        "Perform",           // 11
        "Persuade",          // 12 (Diplomacy in some versions)
        "Pick Pocket",       // 13
        "Search",            // 14
        "Set Trap",          // 15
        "Spellcraft",        // 16
        "Spot",              // 17
        "Taunt",             // 18
        "Use Magic Device",  // 19
        "Appraise",          // 20
        "Tumble",            // 21
        "Craft Trap",        // 22
        "Bluff",             // 23
        "Intimidate",        // 24
        "Craft Armor",       // 25
        "Craft Weapon",      // 26
        "Ride"               // 27
    };

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

        if (_skillsList != null)
            _skillsList.ItemsSource = _skills;
    }

    public void LoadCreature(UtcFile? creature)
    {
        _skills.Clear();

        if (creature == null || creature.SkillList.Count == 0)
        {
            ClearPanel();
            return;
        }

        // Load all skills (show all, even those with 0 ranks)
        for (int i = 0; i < creature.SkillList.Count && i < SkillNames.Length; i++)
        {
            var ranks = creature.SkillList[i];
            _skills.Add(new SkillViewModel
            {
                SkillId = i,
                SkillName = SkillNames[i],
                Ranks = ranks,
                RanksDisplay = ranks > 0 ? ranks.ToString() : "-"
            });
        }

        // Sort by ranks (highest first), then by name
        var sorted = _skills.OrderByDescending(s => s.Ranks).ThenBy(s => s.SkillName).ToList();
        _skills.Clear();
        foreach (var skill in sorted)
            _skills.Add(skill);

        // Update summary
        var skillsWithRanks = _skills.Count(s => s.Ranks > 0);
        var totalRanks = _skills.Sum(s => s.Ranks);
        SetText(_skillsSummaryText, $"{skillsWithRanks} skills with ranks ({totalRanks} total ranks)");

        if (_noSkillsText != null)
            _noSkillsText.IsVisible = false;
    }

    public void ClearPanel()
    {
        _skills.Clear();
        SetText(_skillsSummaryText, "0 skills with ranks");
        if (_noSkillsText != null)
            _noSkillsText.IsVisible = true;
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}

public class SkillViewModel
{
    public int SkillId { get; set; }
    public string SkillName { get; set; } = "";
    public int Ranks { get; set; }
    public string RanksDisplay { get; set; } = "-";
}
