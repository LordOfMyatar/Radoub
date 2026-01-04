using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Quartermaster.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Utc;
using Radoub.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class ScriptsPanel : BasePanelControl
{
    private TextBlock? _scriptsSummaryText;
    private ItemsControl? _scriptsList;
    private TextBlock? _noScriptsText;
    private TextBox? _conversationTextBox;
    private Button? _browseConversationButton;
    private Button? _clearConversationButton;
    private Button? _openInParleyButton;

    private ObservableCollection<ScriptViewModel> _scripts = new();
    private string? _currentFilePath;
    private IGameDataService? _gameDataService;
    private Func<string, string?>? _resolveConversationPath;

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
        _browseConversationButton = this.FindControl<Button>("BrowseConversationButton");
        _clearConversationButton = this.FindControl<Button>("ClearConversationButton");
        _openInParleyButton = this.FindControl<Button>("OpenInParleyButton");

        if (_scriptsList != null)
        {
            _scriptsList.ItemsSource = _scripts;
            _scriptsList.AddHandler(Button.ClickEvent, OnScriptButtonClick);
        }

        if (_conversationTextBox != null)
            _conversationTextBox.TextChanged += OnConversationTextChanged;

        if (_browseConversationButton != null)
            _browseConversationButton.Click += OnBrowseConversationClick;

        if (_clearConversationButton != null)
            _clearConversationButton.Click += OnClearConversationClick;

        if (_openInParleyButton != null)
            _openInParleyButton.Click += OnOpenInParleyClick;
    }

    public void SetGameDataService(IGameDataService? gameDataService)
    {
        _gameDataService = gameDataService;
    }

    public void SetCurrentFilePath(string? filePath)
    {
        _currentFilePath = filePath;
    }

    public void SetConversationResolver(Func<string, string?> resolver)
    {
        _resolveConversationPath = resolver;
    }

    public override void LoadCreature(UtcFile? creature)
    {
        IsLoading = true;
        _scripts.Clear();
        CurrentCreature = creature;

        if (creature == null)
        {
            ClearPanel();
            IsLoading = false;
            return;
        }

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

        SetTextBox(_conversationTextBox, creature.Conversation ?? "");
        UpdateParleyButtonVisibility();

        DeferLoadingReset();
    }

    private void AddScript(string eventName, string scriptResRef, string fieldName)
    {
        var vm = new ScriptViewModel
        {
            EventName = eventName,
            ScriptResRef = scriptResRef ?? "",
            FieldName = fieldName,
            AutomationId = $"Script_{fieldName}",
            BrowseButtonId = $"Browse_{fieldName}",
            ClearButtonId = $"Clear_{fieldName}"
        };

        vm.PropertyChanged += OnScriptPropertyChanged;
        _scripts.Add(vm);
    }

    private void OnScriptPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null || sender is not ScriptViewModel vm)
            return;

        if (e.PropertyName == nameof(ScriptViewModel.ScriptResRef))
        {
            UpdateCreatureScript(vm.FieldName, vm.ScriptResRef);
            UpdateSummary();
            ScriptsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateCreatureScript(string fieldName, string value)
    {
        if (CurrentCreature == null) return;

        switch (fieldName)
        {
            case nameof(UtcFile.ScriptSpawn): CurrentCreature.ScriptSpawn = value; break;
            case nameof(UtcFile.ScriptHeartbeat): CurrentCreature.ScriptHeartbeat = value; break;
            case nameof(UtcFile.ScriptOnNotice): CurrentCreature.ScriptOnNotice = value; break;
            case nameof(UtcFile.ScriptDialogue): CurrentCreature.ScriptDialogue = value; break;
            case nameof(UtcFile.ScriptAttacked): CurrentCreature.ScriptAttacked = value; break;
            case nameof(UtcFile.ScriptDamaged): CurrentCreature.ScriptDamaged = value; break;
            case nameof(UtcFile.ScriptDeath): CurrentCreature.ScriptDeath = value; break;
            case nameof(UtcFile.ScriptDisturbed): CurrentCreature.ScriptDisturbed = value; break;
            case nameof(UtcFile.ScriptEndRound): CurrentCreature.ScriptEndRound = value; break;
            case nameof(UtcFile.ScriptOnBlocked): CurrentCreature.ScriptOnBlocked = value; break;
            case nameof(UtcFile.ScriptRested): CurrentCreature.ScriptRested = value; break;
            case nameof(UtcFile.ScriptSpellAt): CurrentCreature.ScriptSpellAt = value; break;
            case nameof(UtcFile.ScriptUserDefine): CurrentCreature.ScriptUserDefine = value; break;
        }
    }

    private void OnScriptButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button || button.Tag is not string fieldName)
            return;

        var automationId = button.GetValue(Avalonia.Automation.AutomationProperties.AutomationIdProperty);

        if (automationId?.StartsWith("Browse_") == true)
        {
            OnBrowseScriptClick(fieldName);
        }
        else if (automationId?.StartsWith("Clear_") == true)
        {
            var vm = _scripts.FirstOrDefault(s => s.FieldName == fieldName);
            if (vm != null)
                vm.ScriptResRef = "";
        }
    }

    private async void OnBrowseScriptClick(string fieldName)
    {
        var vm = _scripts.FirstOrDefault(s => s.FieldName == fieldName);
        if (vm == null) return;

        var context = new QuartermasterScriptBrowserContext(_currentFilePath, _gameDataService);
        var browser = new ScriptBrowserWindow(context);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var result = await browser.ShowDialog<string?>(parentWindow);
            if (!string.IsNullOrEmpty(result))
                vm.ScriptResRef = result;
        }
    }

    private void OnConversationTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null || _conversationTextBox == null)
            return;

        CurrentCreature.Conversation = _conversationTextBox.Text ?? "";
        UpdateParleyButtonVisibility();
        ScriptsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnBrowseConversationClick(object? sender, RoutedEventArgs e)
    {
        var context = new QuartermasterScriptBrowserContext(_currentFilePath, _gameDataService);
        var browser = new DialogBrowserWindow(context);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var result = await browser.ShowDialog<string?>(parentWindow);
            if (!string.IsNullOrEmpty(result) && _conversationTextBox != null)
                _conversationTextBox.Text = result;
        }
    }

    private void OnClearConversationClick(object? sender, RoutedEventArgs e)
    {
        if (_conversationTextBox != null)
            _conversationTextBox.Text = "";
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

    public override void ClearPanel()
    {
        IsLoading = true;
        foreach (var vm in _scripts)
            vm.PropertyChanged -= OnScriptPropertyChanged;

        _scripts.Clear();
        CurrentCreature = null;
        SetText(_scriptsSummaryText, "0 of 13 scripts assigned");

        if (_noScriptsText != null)
            _noScriptsText.IsVisible = true;
        if (_conversationTextBox != null)
            _conversationTextBox.Text = "";
        if (_openInParleyButton != null)
            _openInParleyButton.IsVisible = false;

        IsLoading = false;
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

        string? dialogPath = null;
        if (_resolveConversationPath != null)
            dialogPath = _resolveConversationPath(conversation);

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
}

/// <summary>
/// ViewModel for a single script event slot.
/// </summary>
public class ScriptViewModel : BindableBase
{
    private string _scriptResRef = "";

    public string EventName { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string AutomationId { get; set; } = "";
    public string BrowseButtonId { get; set; } = "";
    public string ClearButtonId { get; set; } = "";

    public string ScriptResRef
    {
        get => _scriptResRef;
        set => SetProperty(ref _scriptResRef, value);
    }
}
