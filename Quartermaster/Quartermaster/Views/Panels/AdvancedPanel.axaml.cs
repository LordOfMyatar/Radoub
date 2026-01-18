using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Quartermaster.Views.Dialogs;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

public partial class AdvancedPanel : BasePanelControl
{
    // Identity section
    private TextBox? _templateResRefTextBox;
    private TextBox? _tagTextBox;
    private TextBox? _commentTextBox;
    private Button? _copyResRefButton;
    private Button? _copyTagButton;
    private Grid? _resRefRow;
    private Grid? _commentRow;

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

    private CreatureDisplayService? _displayService;
    private string? _currentModuleDirectory;

    public event EventHandler? CommentChanged;
    public event EventHandler? TagChanged;
    public event EventHandler? FlagsChanged;
    public event EventHandler? BehaviorChanged;

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
        _commentTextBox = this.FindControl<TextBox>("CommentTextBox");
        _copyResRefButton = this.FindControl<Button>("CopyResRefButton");
        _copyTagButton = this.FindControl<Button>("CopyTagButton");
        _resRefRow = this.FindControl<Grid>("ResRefRow");
        _commentRow = this.FindControl<Grid>("CommentRow");

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

        // Wire up events
        if (_copyResRefButton != null)
            _copyResRefButton.Click += OnCopyResRefClick;
        if (_copyTagButton != null)
            _copyTagButton.Click += OnCopyTagClick;
        if (_tagTextBox != null)
            _tagTextBox.TextChanged += OnTagTextChanged;
        if (_commentTextBox != null)
            _commentTextBox.TextChanged += OnCommentTextChanged;

        WireUpFlagCheckboxes();
        WireUpBehaviorCombos();
    }

    private void WireUpBehaviorCombos()
    {
        if (_factionBrowseButton != null)
            _factionBrowseButton.Click += OnFactionBrowseClick;
        if (_perceptionComboBox != null)
            _perceptionComboBox.SelectionChanged += OnPerceptionSelectionChanged;
        if (_walkRateComboBox != null)
            _walkRateComboBox.SelectionChanged += OnWalkRateSelectionChanged;
        if (_decayTimeComboBox != null)
            _decayTimeComboBox.SelectionChanged += OnDecayTimeSelectionChanged;
        if (_bodyBagComboBox != null)
            _bodyBagComboBox.SelectionChanged += OnBodyBagSelectionChanged;
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
            if (cb != null)
            {
                cb.IsCheckedChanged += (s, e) =>
                {
                    if (!IsLoading && CurrentCreature != null)
                    {
                        setter(cb.IsChecked ?? false);
                        FlagsChanged?.Invoke(this, EventArgs.Empty);
                    }
                };
            }
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
        LoadBehaviorData();
    }

    /// <summary>
    /// Set whether the current file is a BIC (player character) or UTC (creature blueprint).
    /// This controls visibility of UTC-only fields like Blueprint ResRef and Comment,
    /// and disables fields that shouldn't be editable for BIC files.
    /// </summary>
    public void SetFileType(bool isBicFile)
    {
        // Hide UTC-only fields for BIC files
        if (_resRefRow != null)
            _resRefRow.IsVisible = !isBicFile;
        if (_commentRow != null)
            _commentRow.IsVisible = !isBicFile;

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
}
