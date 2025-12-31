using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.Formats.Utc;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Quartermaster.Views.Panels;

public partial class ScriptsPanel : UserControl
{
    private TextBlock? _scriptsSummaryText;
    private ItemsControl? _scriptsList;
    private TextBlock? _noScriptsText;
    private TextBox? _conversationTextBox;
    private Button? _clearConversationButton;
    private Button? _openInParleyButton;

    private ObservableCollection<ScriptViewModel> _scripts = new();
    private UtcFile? _currentCreature;
    private Func<string, string?>? _resolveConversationPath;
    private bool _isLoading;

    /// <summary>
    /// Raised when any script value is modified.
    /// </summary>
    public event EventHandler? ScriptsChanged;

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
        _conversationTextBox = this.FindControl<TextBox>("ConversationTextBox");
        _clearConversationButton = this.FindControl<Button>("ClearConversationButton");
        _openInParleyButton = this.FindControl<Button>("OpenInParleyButton");

        if (_scriptsList != null)
        {
            _scriptsList.ItemsSource = _scripts;
            _scriptsList.AddHandler(Button.ClickEvent, OnClearScriptClick);
        }

        if (_conversationTextBox != null)
        {
            _conversationTextBox.TextChanged += OnConversationTextChanged;
        }

        if (_clearConversationButton != null)
        {
            _clearConversationButton.Click += OnClearConversationClick;
        }

        if (_openInParleyButton != null)
        {
            _openInParleyButton.Click += OnOpenInParleyClick;
        }
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
        _isLoading = true;
        _scripts.Clear();
        _currentCreature = creature;

        if (creature == null)
        {
            ClearPanel();
            _isLoading = false;
            return;
        }

        // Add all script events with field references for updating
        AddScript("OnSpawnIn", creature.ScriptSpawn, nameof(UtcFile.ScriptSpawn));
        AddScript("OnHeartbeat", creature.ScriptHeartbeat, nameof(UtcFile.ScriptHeartbeat));
        AddScript("OnPerception", creature.ScriptOnNotice, nameof(UtcFile.ScriptOnNotice));
        AddScript("OnConversation", creature.ScriptDialogue, nameof(UtcFile.ScriptDialogue));
        AddScript("OnPhysicalAttacked", creature.ScriptAttacked, nameof(UtcFile.ScriptAttacked));
        AddScript("OnDamaged", creature.ScriptDamaged, nameof(UtcFile.ScriptDamaged));
        AddScript("OnDeath", creature.ScriptDeath, nameof(UtcFile.ScriptDeath));
        AddScript("OnDisturbed", creature.ScriptDisturbed, nameof(UtcFile.ScriptDisturbed));
        AddScript("OnEndCombatRound", creature.ScriptEndRound, nameof(UtcFile.ScriptEndRound));
        AddScript("OnBlocked", creature.ScriptOnBlocked, nameof(UtcFile.ScriptOnBlocked));
        AddScript("OnRested", creature.ScriptRested, nameof(UtcFile.ScriptRested));
        AddScript("OnSpellCastAt", creature.ScriptSpellAt, nameof(UtcFile.ScriptSpellAt));
        AddScript("OnUserDefined", creature.ScriptUserDefine, nameof(UtcFile.ScriptUserDefine));

        UpdateSummary();

        // Conversation
        if (_conversationTextBox != null)
        {
            _conversationTextBox.Text = creature.Conversation ?? "";
        }
        UpdateParleyButtonVisibility();

        _isLoading = false;
    }

    private void AddScript(string eventName, string scriptResRef, string fieldName)
    {
        var vm = new ScriptViewModel
        {
            EventName = eventName,
            ScriptResRef = scriptResRef ?? "",
            FieldName = fieldName,
            AutomationId = $"Script_{fieldName}",
            ClearButtonId = $"Clear_{fieldName}"
        };

        // Subscribe to property changes to update creature and fire event
        vm.PropertyChanged += OnScriptPropertyChanged;

        _scripts.Add(vm);
    }

    private void OnScriptPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null || sender is not ScriptViewModel vm)
            return;

        if (e.PropertyName == nameof(ScriptViewModel.ScriptResRef))
        {
            // Update the creature's script field
            UpdateCreatureScript(vm.FieldName, vm.ScriptResRef);
            UpdateSummary();
            ScriptsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateCreatureScript(string fieldName, string value)
    {
        if (_currentCreature == null)
            return;

        switch (fieldName)
        {
            case nameof(UtcFile.ScriptSpawn):
                _currentCreature.ScriptSpawn = value;
                break;
            case nameof(UtcFile.ScriptHeartbeat):
                _currentCreature.ScriptHeartbeat = value;
                break;
            case nameof(UtcFile.ScriptOnNotice):
                _currentCreature.ScriptOnNotice = value;
                break;
            case nameof(UtcFile.ScriptDialogue):
                _currentCreature.ScriptDialogue = value;
                break;
            case nameof(UtcFile.ScriptAttacked):
                _currentCreature.ScriptAttacked = value;
                break;
            case nameof(UtcFile.ScriptDamaged):
                _currentCreature.ScriptDamaged = value;
                break;
            case nameof(UtcFile.ScriptDeath):
                _currentCreature.ScriptDeath = value;
                break;
            case nameof(UtcFile.ScriptDisturbed):
                _currentCreature.ScriptDisturbed = value;
                break;
            case nameof(UtcFile.ScriptEndRound):
                _currentCreature.ScriptEndRound = value;
                break;
            case nameof(UtcFile.ScriptOnBlocked):
                _currentCreature.ScriptOnBlocked = value;
                break;
            case nameof(UtcFile.ScriptRested):
                _currentCreature.ScriptRested = value;
                break;
            case nameof(UtcFile.ScriptSpellAt):
                _currentCreature.ScriptSpellAt = value;
                break;
            case nameof(UtcFile.ScriptUserDefine):
                _currentCreature.ScriptUserDefine = value;
                break;
        }
    }

    private void OnClearScriptClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button button && button.Tag is string fieldName)
        {
            var vm = _scripts.FirstOrDefault(s => s.FieldName == fieldName);
            if (vm != null)
            {
                vm.ScriptResRef = "";
            }
        }
    }

    private void OnConversationTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null || _conversationTextBox == null)
            return;

        _currentCreature.Conversation = _conversationTextBox.Text ?? "";
        UpdateParleyButtonVisibility();
        ScriptsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnClearConversationClick(object? sender, RoutedEventArgs e)
    {
        if (_conversationTextBox != null)
        {
            _conversationTextBox.Text = "";
        }
    }

    private void UpdateParleyButtonVisibility()
    {
        if (_openInParleyButton == null || _conversationTextBox == null)
            return;

        var hasConversation = !string.IsNullOrEmpty(_conversationTextBox.Text);
        var parleyConfigured = !string.IsNullOrEmpty(RadoubSettings.Instance.ParleyPath)
                               && File.Exists(RadoubSettings.Instance.ParleyPath);
        _openInParleyButton.IsVisible = hasConversation && parleyConfigured;
    }

    private void UpdateSummary()
    {
        var assignedCount = _scripts.Count(s => !string.IsNullOrEmpty(s.ScriptResRef));
        SetText(_scriptsSummaryText, $"{assignedCount} of 13 scripts assigned");

        if (_noScriptsText != null)
            _noScriptsText.IsVisible = assignedCount == 0;
    }

    public void ClearPanel()
    {
        _isLoading = true;
        foreach (var vm in _scripts)
        {
            vm.PropertyChanged -= OnScriptPropertyChanged;
        }
        _scripts.Clear();
        _currentCreature = null;
        SetText(_scriptsSummaryText, "0 of 13 scripts assigned");
        if (_noScriptsText != null)
            _noScriptsText.IsVisible = true;
        if (_conversationTextBox != null)
            _conversationTextBox.Text = "";
        if (_openInParleyButton != null)
            _openInParleyButton.IsVisible = false;
        _isLoading = false;
    }

    private void OnOpenInParleyClick(object? sender, RoutedEventArgs e)
    {
        var conversation = _conversationTextBox?.Text;
        if (string.IsNullOrEmpty(conversation))
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
            dialogPath = _resolveConversationPath(conversation);
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
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Opening {conversation}.dlg in Parley");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Launching Parley (dialog file {conversation}.dlg not found locally)");
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

public class ScriptViewModel : INotifyPropertyChanged
{
    private string _scriptResRef = "";

    public string EventName { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string AutomationId { get; set; } = "";
    public string ClearButtonId { get; set; } = "";

    public string ScriptResRef
    {
        get => _scriptResRef;
        set
        {
            if (_scriptResRef != value)
            {
                _scriptResRef = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
