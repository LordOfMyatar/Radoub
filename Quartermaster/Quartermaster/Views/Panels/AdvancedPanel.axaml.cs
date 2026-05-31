using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Quartermaster.ViewModels;
using Quartermaster.Views.Dialogs;
using Radoub.Formats.Services;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using VariableViewModel = Radoub.UI.ViewModels.VariableViewModel;

namespace Quartermaster.Views.Panels;

public partial class AdvancedPanel : BasePanelControl
{
    // Identity section
    private TextBox? _templateResRefTextBox;
    private TextBox? _tagTextBox;
    private Radoub.UI.Controls.SpellCheckTextBox? _commentTextBox;
    private Button? _copyResRefButton;
    private Button? _copyTagButton;
    private Button? _renameResRefButton;
    private Grid? _resRefRow;
    private Grid? _tagRow;
    private Grid? _commentRow;
    private Grid? _paletteCategoryRow;
    private ComboBox? _paletteCategoryComboBox;

    // Flag checkboxes
    private CheckBox? _plotCheckBox;
    private CheckBox? _immortalCheckBox;
    private CheckBox? _noPermDeathCheckBox;
    private CheckBox? _isPCCheckBox;
    private CheckBox? _disarmableCheckBox;
    private CheckBox? _lootableCheckBox;
    private CheckBox? _interruptableCheckBox;

    // Behavior
    private TextBox? _factionTextBox;
    private Button? _factionBrowseButton;
    private ComboBox? _perceptionComboBox;
    private ComboBox? _walkRateComboBox;
    private ComboBox? _decayTimeComboBox;
    private ComboBox? _bodyBagComboBox;

    // Variables (shared Radoub.UI control, #2293)
    private Border? _variablesSection;
    private Radoub.UI.Controls.VariablesPanel? _variablesPanel;

    private CreatureDisplayService? _displayService;
    private string? _currentModuleDirectory;

    /// <summary>
    /// Collection of local variables for data binding.
    /// </summary>
    public ObservableCollection<VariableViewModel> Variables { get; } = new();

    public event EventHandler? CommentChanged;
    public event EventHandler? TagChanged;
    public event EventHandler? FlagsChanged;
    public event EventHandler? BehaviorChanged;
    public event EventHandler? PaletteCategoryChanged;
    public event EventHandler? VariablesChanged;
    public event EventHandler? RenameRequested;

    // All event handler subscriptions tracked so we can release them on Unloaded
    // and let the prior CurrentCreature graph be garbage-collected (#2034).
    private readonly EventSubscriptions _subs = new();

    public AdvancedPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Identity section
        _templateResRefTextBox = this.FindControl<TextBox>("TemplateResRefTextBox");
        _tagTextBox = this.FindControl<TextBox>("TagTextBox");
        _commentTextBox = this.FindControl<Radoub.UI.Controls.SpellCheckTextBox>("CommentTextBox");
        _copyResRefButton = this.FindControl<Button>("CopyResRefButton");
        _copyTagButton = this.FindControl<Button>("CopyTagButton");
        _renameResRefButton = this.FindControl<Button>("RenameResRefButton");
        _resRefRow = this.FindControl<Grid>("ResRefRow");
        _tagRow = this.FindControl<Grid>("TagRow");
        _commentRow = this.FindControl<Grid>("CommentRow");
        _paletteCategoryRow = this.FindControl<Grid>("PaletteCategoryRow");
        _paletteCategoryComboBox = this.FindControl<ComboBox>("PaletteCategoryComboBox");

        // Flag checkboxes
        _plotCheckBox = this.FindControl<CheckBox>("PlotCheckBox");
        _immortalCheckBox = this.FindControl<CheckBox>("ImmortalCheckBox");
        _noPermDeathCheckBox = this.FindControl<CheckBox>("NoPermDeathCheckBox");
        _isPCCheckBox = this.FindControl<CheckBox>("IsPCCheckBox");
        _disarmableCheckBox = this.FindControl<CheckBox>("DisarmableCheckBox");
        _lootableCheckBox = this.FindControl<CheckBox>("LootableCheckBox");
        _interruptableCheckBox = this.FindControl<CheckBox>("InterruptableCheckBox");

        // Behavior
        _factionTextBox = this.FindControl<TextBox>("FactionTextBox");
        _factionBrowseButton = this.FindControl<Button>("FactionBrowseButton");
        _perceptionComboBox = this.FindControl<ComboBox>("PerceptionComboBox");
        _walkRateComboBox = this.FindControl<ComboBox>("WalkRateComboBox");
        _decayTimeComboBox = this.FindControl<ComboBox>("DecayTimeComboBox");
        _bodyBagComboBox = this.FindControl<ComboBox>("BodyBagComboBox");

        // Variables (shared Radoub.UI control, #2293)
        _variablesSection = this.FindControl<Border>("VariablesSection");
        _variablesPanel = this.FindControl<Radoub.UI.Controls.VariablesPanel>("VariablesPanelControl");

        // Wire up events — track every subscription so DetachAll() can release them
        if (_copyResRefButton != null)
            _subs.Track(
                attach: () => _copyResRefButton.Click += OnCopyResRefClick,
                detach: () => _copyResRefButton.Click -= OnCopyResRefClick);
        if (_copyTagButton != null)
            _subs.Track(
                attach: () => _copyTagButton.Click += OnCopyTagClick,
                detach: () => _copyTagButton.Click -= OnCopyTagClick);
        if (_renameResRefButton != null)
            _subs.Track(
                attach: () => _renameResRefButton.Click += OnRenameResRefClick,
                detach: () => _renameResRefButton.Click -= OnRenameResRefClick);
        if (_tagTextBox != null)
            _subs.Track(
                attach: () => _tagTextBox.TextChanged += OnTagTextChanged,
                detach: () => _tagTextBox.TextChanged -= OnTagTextChanged);
        if (_commentTextBox != null)
        {
            _subs.Track(
                attach: () => _commentTextBox.TextChanged += OnCommentTextChanged,
                detach: () => _commentTextBox.TextChanged -= OnCommentTextChanged);
            WireTokenMenu(_commentTextBox);
        }

        WireUpFlagCheckboxes();
        WireUpBehaviorCombos();
        WireUpIdentityCombos();
        WireUpVariables();

        Unloaded += OnPanelUnloaded;
    }

    private void OnPanelUnloaded(object? sender, RoutedEventArgs e)
    {
        Unloaded -= OnPanelUnloaded;
        _subs.DetachAll();
        ClearVariables(); // Releases per-VariableViewModel PropertyChanged subscriptions
    }

    private void WireUpIdentityCombos()
    {
        if (_paletteCategoryComboBox != null)
            _subs.Track(
                attach: () => _paletteCategoryComboBox.SelectionChanged += OnPaletteCategorySelectionChanged,
                detach: () => _paletteCategoryComboBox.SelectionChanged -= OnPaletteCategorySelectionChanged);
    }

    private void OnPaletteCategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        var categoryId = ComboBoxHelper.GetSelectedTag<byte>(_paletteCategoryComboBox);
        if (categoryId.HasValue)
        {
            CurrentCreature.PaletteID = categoryId.Value;
            PaletteCategoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void WireUpBehaviorCombos()
    {
        if (_factionBrowseButton != null)
            _subs.Track(
                attach: () => _factionBrowseButton.Click += OnFactionBrowseClick,
                detach: () => _factionBrowseButton.Click -= OnFactionBrowseClick);
        if (_perceptionComboBox != null)
            _subs.Track(
                attach: () => _perceptionComboBox.SelectionChanged += OnPerceptionSelectionChanged,
                detach: () => _perceptionComboBox.SelectionChanged -= OnPerceptionSelectionChanged);
        if (_walkRateComboBox != null)
            _subs.Track(
                attach: () => _walkRateComboBox.SelectionChanged += OnWalkRateSelectionChanged,
                detach: () => _walkRateComboBox.SelectionChanged -= OnWalkRateSelectionChanged);
        if (_decayTimeComboBox != null)
            _subs.Track(
                attach: () => _decayTimeComboBox.SelectionChanged += OnDecayTimeSelectionChanged,
                detach: () => _decayTimeComboBox.SelectionChanged -= OnDecayTimeSelectionChanged);
        if (_bodyBagComboBox != null)
            _subs.Track(
                attach: () => _bodyBagComboBox.SelectionChanged += OnBodyBagSelectionChanged,
                detach: () => _bodyBagComboBox.SelectionChanged -= OnBodyBagSelectionChanged);
    }

    private async void OnFactionBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (CurrentCreature == null || _displayService == null) return;

        var factions = _displayService.GetAllFactions(_currentModuleDirectory);
        var picker = new FactionPickerWindow(factions, CurrentCreature.FactionID);

        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow != null)
        {
            await picker.ShowDialog(parentWindow);
            if (picker.Confirmed && picker.SelectedFactionId.HasValue)
            {
                CurrentCreature.FactionID = picker.SelectedFactionId.Value;
                UpdateFactionDisplay();
                BehaviorChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnPerceptionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        var perception = ComboBoxHelper.GetSelectedTag<byte>(_perceptionComboBox);
        if (perception.HasValue)
        {
            CurrentCreature.PerceptionRange = perception.Value;
            BehaviorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnWalkRateSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        var walkRate = ComboBoxHelper.GetSelectedTag<int>(_walkRateComboBox);
        if (walkRate.HasValue)
        {
            CurrentCreature.WalkRate = walkRate.Value;
            BehaviorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDecayTimeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        var decayTime = ComboBoxHelper.GetSelectedTag<uint>(_decayTimeComboBox);
        if (decayTime.HasValue)
        {
            CurrentCreature.DecayTime = decayTime.Value;
            BehaviorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnBodyBagSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        var bodyBag = ComboBoxHelper.GetSelectedTag<byte>(_bodyBagComboBox);
        if (bodyBag.HasValue)
        {
            CurrentCreature.BodyBag = bodyBag.Value;
            BehaviorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void WireUpFlagCheckboxes()
    {
        void WireFlag(CheckBox? cb, Action<bool> setter)
        {
            if (cb == null) return;

            // Capture the handler in a local so we can detach it on Unloaded —
            // the previous inline lambda was unreachable and held `this` +
            // `CurrentCreature` alive for the panel's lifetime (#2034).
            void Handler(object? s, RoutedEventArgs e)
            {
                if (!IsLoading && CurrentCreature != null)
                {
                    setter(cb.IsChecked ?? false);
                    FlagsChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            _subs.Track(
                attach: () => cb.IsCheckedChanged += Handler,
                detach: () => cb.IsCheckedChanged -= Handler);
        }

        WireFlag(_plotCheckBox, v => { if (CurrentCreature != null) CurrentCreature.Plot = v; });
        WireFlag(_immortalCheckBox, v => { if (CurrentCreature != null) CurrentCreature.IsImmortal = v; });
        WireFlag(_noPermDeathCheckBox, v => { if (CurrentCreature != null) CurrentCreature.NoPermDeath = v; });
        WireFlag(_isPCCheckBox, v => { if (CurrentCreature != null) CurrentCreature.IsPC = v; });
        WireFlag(_disarmableCheckBox, v => { if (CurrentCreature != null) CurrentCreature.Disarmable = v; });
        WireFlag(_lootableCheckBox, v => { if (CurrentCreature != null) CurrentCreature.Lootable = v; });
        WireFlag(_interruptableCheckBox, v => { if (CurrentCreature != null) CurrentCreature.Interruptable = v; });
    }

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
        // LoadBehaviorData is just hardcoded combo population — keep on UI thread.
        LoadBehaviorData();
        // LoadPaletteCategoryData hits creaturepal.itp via GameDataService — push to
        // background to keep ~700ms off the UI thread on startup (#2058).
        _ = LoadPaletteCategoryDataInBackgroundAsync(displayService);
    }

    private async System.Threading.Tasks.Task LoadPaletteCategoryDataInBackgroundAsync(CreatureDisplayService displayService)
    {
        try
        {
            var categories = await System.Threading.Tasks.Task.Run(() =>
                displayService.GetCreaturePaletteCategories().ToList());

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                PopulatePaletteCategoryCombo(categories);
                // If a creature was loaded before the combo populated, re-select its category.
                if (CurrentCreature != null)
                {
                    ComboBoxHelper.SelectByTag(_paletteCategoryComboBox, CurrentCreature.PaletteID);
                }
            });
        }
        catch (Exception ex)
        {
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.WARN,
                $"AdvancedPanel: palette category background load failed — {ex.Message}");
        }
    }

    /// <summary>
    /// Set whether the current file is a BIC (player character) or UTC (creature blueprint).
    /// This controls visibility of UTC-only fields like Blueprint ResRef, Comment, Palette Category, and Variables,
    /// and disables fields that shouldn't be editable for BIC files.
    /// </summary>
    public void SetFileType(bool isBicFile)
    {
        // Hide UTC-only fields for BIC files
        if (_resRefRow != null)
            _resRefRow.IsVisible = !isBicFile;
        if (_tagRow != null)
            _tagRow.IsVisible = !isBicFile;
        if (_commentRow != null)
            _commentRow.IsVisible = !isBicFile;
        if (_paletteCategoryRow != null)
            _paletteCategoryRow.IsVisible = !isBicFile;
        if (_variablesSection != null)
            _variablesSection.IsVisible = !isBicFile;

        // IsPC should always be true for BIC files and not editable
        if (_isPCCheckBox != null)
        {
            _isPCCheckBox.IsEnabled = !isBicFile;
            if (isBicFile)
                _isPCCheckBox.IsChecked = true;
        }
    }

    /// <summary>
    /// Set the module directory to load factions from repute.fac.
    /// </summary>
    public void SetModuleDirectory(string? moduleDirectory)
    {
        _currentModuleDirectory = moduleDirectory;
    }

    private void UpdateFactionDisplay()
    {
        if (_factionTextBox == null || CurrentCreature == null) return;

        var factionId = CurrentCreature.FactionID;

        if (_displayService != null)
        {
            var factions = _displayService.GetAllFactions(_currentModuleDirectory);
            var faction = factions.FirstOrDefault(f => f.Id == factionId);
            if (!string.IsNullOrEmpty(faction.Name))
            {
                _factionTextBox.Text = $"{faction.Name} ({factionId})";
                return;
            }
        }

        _factionTextBox.Text = factionId.ToString();
    }

    private void LoadBehaviorData()
    {
        if (_perceptionComboBox != null)
        {
            _perceptionComboBox.Items.Clear();
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Short (9)", Tag = (byte)9 });
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Medium (10)", Tag = (byte)10 });
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Normal (11)", Tag = (byte)11 });
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Long (12)", Tag = (byte)12 });
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Maximum (13)", Tag = (byte)13 });
        }

        if (_walkRateComboBox != null)
        {
            _walkRateComboBox.Items.Clear();
            _walkRateComboBox.Items.Add(new ComboBoxItem { Content = "PC (0)", Tag = 0 });
            _walkRateComboBox.Items.Add(new ComboBoxItem { Content = "Immobile (1)", Tag = 1 });
            _walkRateComboBox.Items.Add(new ComboBoxItem { Content = "Very Slow (2)", Tag = 2 });
            _walkRateComboBox.Items.Add(new ComboBoxItem { Content = "Slow (3)", Tag = 3 });
            _walkRateComboBox.Items.Add(new ComboBoxItem { Content = "Normal (4)", Tag = 4 });
            _walkRateComboBox.Items.Add(new ComboBoxItem { Content = "Fast (5)", Tag = 5 });
            _walkRateComboBox.Items.Add(new ComboBoxItem { Content = "Very Fast (6)", Tag = 6 });
            _walkRateComboBox.Items.Add(new ComboBoxItem { Content = "Default (7)", Tag = 7 });
        }

        if (_decayTimeComboBox != null)
        {
            _decayTimeComboBox.Items.Clear();
            _decayTimeComboBox.Items.Add(new ComboBoxItem { Content = "Instant (0 ms)", Tag = 0u });
            _decayTimeComboBox.Items.Add(new ComboBoxItem { Content = "1 second", Tag = 1000u });
            _decayTimeComboBox.Items.Add(new ComboBoxItem { Content = "5 seconds (Default)", Tag = 5000u });
            _decayTimeComboBox.Items.Add(new ComboBoxItem { Content = "10 seconds", Tag = 10000u });
            _decayTimeComboBox.Items.Add(new ComboBoxItem { Content = "30 seconds", Tag = 30000u });
            _decayTimeComboBox.Items.Add(new ComboBoxItem { Content = "1 minute", Tag = 60000u });
            _decayTimeComboBox.Items.Add(new ComboBoxItem { Content = "5 minutes", Tag = 300000u });
        }

        if (_bodyBagComboBox != null)
        {
            _bodyBagComboBox.Items.Clear();
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "Default (0)", Tag = (byte)0 });
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "Body Bag (1)", Tag = (byte)1 });
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "Pouch (2)", Tag = (byte)2 });
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "Treasure Pile (3)", Tag = (byte)3 });
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "No Body (4)", Tag = (byte)4 });
        }
    }

    private void PopulatePaletteCategoryCombo(System.Collections.Generic.List<PaletteCategory> categories)
    {
        if (_paletteCategoryComboBox == null) return;

        _paletteCategoryComboBox.Items.Clear();

        if (categories.Count == 0)
        {
            // Fallback if no categories loaded (e.g., game data not configured)
            _paletteCategoryComboBox.Items.Add(new ComboBoxItem { Content = "Custom (1)", Tag = (byte)1 });
            return;
        }

        foreach (var category in categories.OrderBy(c => c.Id))
        {
            var displayName = !string.IsNullOrEmpty(category.ParentPath)
                ? $"{category.ParentPath}/{category.Name} ({category.Id})"
                : $"{category.Name} ({category.Id})";

            _paletteCategoryComboBox.Items.Add(new ComboBoxItem
            {
                Content = displayName,
                Tag = category.Id
            });
        }
    }

    public override void LoadCreature(UtcFile? creature)
    {
        IsLoading = true;
        CurrentCreature = creature;

        if (creature == null)
        {
            ClearPanel();
            IsLoading = false;
            return;
        }

        // Identity
        SetTextBox(_templateResRefTextBox, creature.TemplateResRef ?? "");
        SetTextBox(_tagTextBox, creature.Tag ?? "");
        SetTextBox(_commentTextBox, creature.Comment ?? "");
        ComboBoxHelper.SelectByTag(_paletteCategoryComboBox, creature.PaletteID);

        // Flags
        SetCheckBox(_plotCheckBox, creature.Plot);
        SetCheckBox(_immortalCheckBox, creature.IsImmortal);
        SetCheckBox(_noPermDeathCheckBox, creature.NoPermDeath);
        SetCheckBox(_isPCCheckBox, creature.IsPC);
        SetCheckBox(_disarmableCheckBox, creature.Disarmable);
        SetCheckBox(_lootableCheckBox, creature.Lootable);
        SetCheckBox(_interruptableCheckBox, creature.Interruptable);

        // Behavior
        UpdateFactionDisplay();
        ComboBoxHelper.SelectByTag(_perceptionComboBox, creature.PerceptionRange);
        ComboBoxHelper.SelectByTag(_walkRateComboBox, creature.WalkRate);
        ComboBoxHelper.SelectByTag(_decayTimeComboBox, creature.DecayTime, "{0} ms");
        ComboBoxHelper.SelectByTag(_bodyBagComboBox, creature.BodyBag);

        // Variables
        PopulateVariables();

        DeferLoadingReset();
    }

    public override void ClearPanel()
    {
        SetTextBox(_templateResRefTextBox, "");
        SetTextBox(_tagTextBox, "");
        SetTextBox(_commentTextBox, "");

        SetCheckBox(_plotCheckBox, false);
        SetCheckBox(_immortalCheckBox, false);
        SetCheckBox(_noPermDeathCheckBox, false);
        SetCheckBox(_isPCCheckBox, false);
        SetCheckBox(_disarmableCheckBox, false);
        SetCheckBox(_lootableCheckBox, false);
        SetCheckBox(_interruptableCheckBox, true); // Default

        SetTextBox(_factionTextBox, "");
        if (_perceptionComboBox != null)
            _perceptionComboBox.SelectedIndex = 2; // Normal (11)
        if (_walkRateComboBox != null)
            _walkRateComboBox.SelectedIndex = 0;
        if (_decayTimeComboBox != null)
            _decayTimeComboBox.SelectedIndex = 2; // 5 seconds default
        if (_bodyBagComboBox != null)
            _bodyBagComboBox.SelectedIndex = 0;
        // Select PaletteID 1 (typically Custom category) by tag, not index
        ComboBoxHelper.SelectByTag(_paletteCategoryComboBox, (byte)1);

        // Variables
        ClearVariables();
    }

    private async void OnCopyResRefClick(object? sender, RoutedEventArgs e)
    {
        var text = _templateResRefTextBox?.Text;
        if (!string.IsNullOrEmpty(text))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }
    }

    private async void OnCopyTagClick(object? sender, RoutedEventArgs e)
    {
        var text = _tagTextBox?.Text;
        if (!string.IsNullOrEmpty(text))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }
    }

    private void OnRenameResRefClick(object? sender, RoutedEventArgs e)
    {
        RenameRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the ResRef display after a successful rename.
    /// </summary>
    public void UpdateResRefDisplay(string newResRef)
    {
        if (_templateResRefTextBox != null)
            _templateResRefTextBox.Text = newResRef;
    }

    private void OnTagTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        CurrentCreature.Tag = _tagTextBox?.Text ?? "";
        TagChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCommentTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        CurrentCreature.Comment = _commentTextBox?.Text ?? "";
        CommentChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetComment() => _commentTextBox?.Text ?? "";
    public string GetTag() => _tagTextBox?.Text ?? "";

    #region Variables

    private bool _variablesPanelWired;

    private void WireUpVariables()
    {
        if (_variablesPanel == null || _variablesPanelWired) return;
        _variablesPanelWired = true;

        _variablesPanel.Variables = Variables;
        _subs.Track(
            attach: () => _variablesPanel.AddRequested += OnVariableAddRequested,
            detach: () => _variablesPanel.AddRequested -= OnVariableAddRequested);
        _subs.Track(
            attach: () => _variablesPanel.DeleteRequested += OnVariableDeleteRequested,
            detach: () => _variablesPanel.DeleteRequested -= OnVariableDeleteRequested);
        // Panel self-validates and raises VariablesChanged only on real user edits.
        _subs.Track(
            attach: () => _variablesPanel.VariablesChanged += OnPanelVariablesChanged,
            detach: () => _variablesPanel.VariablesChanged -= OnPanelVariablesChanged);
    }

    /// <summary>
    /// Populate the Variables collection from the current creature's VarTable.
    /// </summary>
    private void PopulateVariables()
    {
        Variables.Clear();

        if (CurrentCreature == null) return;

        foreach (var variable in CurrentCreature.VarTable)
            Variables.Add(VariableViewModel.FromVariable(variable));

        _variablesPanel?.RevalidateNames();
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Loaded {Variables.Count} local variables");
    }

    private void OnPanelVariablesChanged(object? sender, EventArgs e)
    {
        if (IsLoading) return;
        VariablesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Update the current creature's VarTable from the Variables collection.
    /// Call this before saving.
    /// </summary>
    /// <summary>
    /// Update the current creature's VarTable from the Variables collection.
    /// Empty-name variables are stripped on save.
    /// </summary>
    public void UpdateVarTable()
    {
        if (CurrentCreature == null) return;

        CurrentCreature.VarTable.Clear();
        foreach (var vm in Variables)
        {
            if (!string.IsNullOrWhiteSpace(vm.Name))
                CurrentCreature.VarTable.Add(vm.ToVariable());
        }
    }

    private void OnVariableAddRequested(object? sender, VariableAddRequestedEventArgs e)
    {
        if (CurrentCreature == null) return;

        var name = VariableViewModel.NextDefaultName(Variables.Select(v => v.Name));
        var newVar = new VariableViewModel { Name = name, Type = VariableType.Int };
        Variables.Add(newVar); // panel auto-validates via CollectionChanged

        if (_variablesPanel != null)
        {
            _variablesPanel.SelectedVariable = newVar;
            _variablesPanel.FocusSelectedName();
        }

        VariablesChanged?.Invoke(this, EventArgs.Empty);
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added new variable: {name}");
    }

    private void OnVariableDeleteRequested(object? sender, VariableDeleteRequestedEventArgs e)
    {
        if (e.Variables.Count == 0) return;

        foreach (var item in e.Variables)
            Variables.Remove(item);

        VariablesChanged?.Invoke(this, EventArgs.Empty);
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed {e.Variables.Count} variable(s)");
    }

    /// <summary>
    /// Returns true if any variable has a duplicate name error.
    /// Empty-name variables are stripped on save and don't block.
    /// </summary>
    public bool HasDuplicateVariableErrors()
    {
        return Variables.Any(v => v.HasError && !string.IsNullOrWhiteSpace(v.Name));
    }

    private void ClearVariables()
    {
        // Panel manages per-item subscriptions internally; just clear the collection.
        Variables.Clear();
    }

    #endregion

    #region Token Support

    private readonly QuickTokenService _quickTokenService = new();

    private void WireTokenMenu(SpellCheckTextBox? textBox)
    {
        if (textBox == null) return;
        textBox.ContextMenuExtras = menu =>
            TokenContextMenu.AppendTokenMenu(menu, textBox, () =>
                _ = TokenInsertionHelper.OpenTokenWindowAsync(textBox, this.VisualRoot as Avalonia.Controls.Window),
                _quickTokenService);
    }

    #endregion
}
