using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.Formats.Utc;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class ScriptsPanel : UserControl
{
    private TextBlock? _scriptsSummaryText;
    private ItemsControl? _scriptsList;
    private TextBlock? _noScriptsText;
    private TextBlock? _conversationText;
    private Button? _openInParleyButton;

    private ObservableCollection<ScriptViewModel> _scripts = new();
    private string? _currentConversation;
    private Func<string, string?>? _resolveConversationPath;

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
        _openInParleyButton = this.FindControl<Button>("OpenInParleyButton");

        if (_scriptsList != null)
            _scriptsList.ItemsSource = _scripts;

        if (_openInParleyButton != null)
            _openInParleyButton.Click += OnOpenInParleyClick;
    }

    /// <summary>
    /// Set a callback to resolve conversation resref to full file path.
    /// </summary>
    public void SetConversationResolver(Func<string, string?> resolver)
    {
        _resolveConversationPath = resolver;
    }

    public void LoadCreature(UtcFile? creature)
    {
        _scripts.Clear();
        _currentConversation = null;

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
        var hasConversation = !string.IsNullOrEmpty(creature.Conversation);
        _currentConversation = hasConversation ? creature.Conversation : null;
        SetText(_conversationText, hasConversation ? creature.Conversation! : "-");

        // Show button only when conversation is assigned and Parley path is configured
        if (_openInParleyButton != null)
        {
            var parleyConfigured = !string.IsNullOrEmpty(RadoubSettings.Instance.ParleyPath)
                                   && File.Exists(RadoubSettings.Instance.ParleyPath);
            _openInParleyButton.IsVisible = hasConversation && parleyConfigured;
        }
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
        _currentConversation = null;
        SetText(_scriptsSummaryText, "0 scripts assigned");
        if (_noScriptsText != null)
            _noScriptsText.IsVisible = true;
        SetText(_conversationText, "-");
        if (_openInParleyButton != null)
            _openInParleyButton.IsVisible = false;
    }

    private void OnOpenInParleyClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentConversation))
            return;

        var parleyPath = RadoubSettings.Instance.ParleyPath;
        if (string.IsNullOrEmpty(parleyPath) || !File.Exists(parleyPath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Parley path not configured or not found");
            return;
        }

        // Resolve conversation resref to file path
        string? dialogPath = null;
        if (_resolveConversationPath != null)
        {
            dialogPath = _resolveConversationPath(_currentConversation);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = parleyPath,
                UseShellExecute = false
            };

            if (!string.IsNullOrEmpty(dialogPath) && File.Exists(dialogPath))
            {
                startInfo.ArgumentList.Add(dialogPath);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Opening {_currentConversation}.dlg in Parley");
            }
            else
            {
                // Launch Parley without file - user can search for it
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Launching Parley (dialog file {_currentConversation}.dlg not found locally)");
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch Parley: {ex.Message}");
        }
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
