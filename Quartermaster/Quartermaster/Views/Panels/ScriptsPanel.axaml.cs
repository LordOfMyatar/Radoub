using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Utc;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class ScriptsPanel : UserControl
{
    private TextBlock? _scriptsSummaryText;
    private ItemsControl? _scriptsList;
    private TextBlock? _noScriptsText;
    private TextBlock? _conversationText;

    private ObservableCollection<ScriptViewModel> _scripts = new();

    public ScriptsPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _scriptsSummaryText = this.FindControl<TextBlock>("ScriptsSummaryText");
        _scriptsList = this.FindControl<ItemsControl>("ScriptsList");
        _noScriptsText = this.FindControl<TextBlock>("NoScriptsText");
        _conversationText = this.FindControl<TextBlock>("ConversationText");

        if (_scriptsList != null)
            _scriptsList.ItemsSource = _scripts;
    }

    public void LoadCreature(UtcFile? creature)
    {
        _scripts.Clear();

        if (creature == null)
        {
            ClearPanel();
            return;
        }

        // Add all script events
        AddScript("OnSpawnIn", creature.ScriptSpawn);
        AddScript("OnHeartbeat", creature.ScriptHeartbeat);
        AddScript("OnPerception", creature.ScriptOnNotice);
        AddScript("OnConversation", creature.ScriptDialogue);
        AddScript("OnPhysicalAttacked", creature.ScriptAttacked);
        AddScript("OnDamaged", creature.ScriptDamaged);
        AddScript("OnDeath", creature.ScriptDeath);
        AddScript("OnInventoryDisturbed", creature.ScriptDisturbed);
        AddScript("OnEndCombatRound", creature.ScriptEndRound);
        AddScript("OnBlocked", creature.ScriptOnBlocked);
        AddScript("OnRested", creature.ScriptRested);
        AddScript("OnSpellCastAt", creature.ScriptSpellAt);
        AddScript("OnUserDefined", creature.ScriptUserDefine);

        var assignedCount = _scripts.Count(s => !string.IsNullOrEmpty(s.ScriptResRef) && s.ScriptResRef != "-");
        SetText(_scriptsSummaryText, $"{assignedCount} scripts assigned");

        if (_noScriptsText != null)
            _noScriptsText.IsVisible = assignedCount == 0;

        // Conversation
        SetText(_conversationText, string.IsNullOrEmpty(creature.Conversation) ? "-" : creature.Conversation);
    }

    private void AddScript(string eventName, string scriptResRef)
    {
        _scripts.Add(new ScriptViewModel
        {
            EventName = eventName,
            ScriptResRef = string.IsNullOrEmpty(scriptResRef) ? "-" : scriptResRef
        });
    }

    public void ClearPanel()
    {
        _scripts.Clear();
        SetText(_scriptsSummaryText, "0 scripts assigned");
        if (_noScriptsText != null)
            _noScriptsText.IsVisible = true;
        SetText(_conversationText, "-");
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}

public class ScriptViewModel
{
    public string EventName { get; set; } = "";
    public string ScriptResRef { get; set; } = "-";
}
