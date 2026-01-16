using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Quartermaster.ViewModels;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;
using Radoub.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class ScriptsPanel : BasePanelControl
{
    private TextBlock? _scriptsSummaryText;
    private ItemsControl? _scriptsList;
    private TextBlock? _noScriptsText;

    private ObservableCollection<ScriptViewModel> _scripts = new();
    private string? _currentFilePath;
    private IGameDataService? _gameDataService;

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

        if (_scriptsList != null)
        {
            _scriptsList.ItemsSource = _scripts;
            _scriptsList.AddHandler(Button.ClickEvent, OnScriptButtonClick);
        }
    }

    public void SetGameDataService(IGameDataService? gameDataService)
    {
        _gameDataService = gameDataService;
    }

    public void SetCurrentFilePath(string? filePath)
    {
        _currentFilePath = filePath;
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

        IsLoading = false;
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
