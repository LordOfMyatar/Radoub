using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

public partial class AdvancedPanel : UserControl
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
    private ComboBox? _factionComboBox;
    private ComboBox? _perceptionComboBox;
    private ComboBox? _walkRateComboBox;
    private ComboBox? _decayTimeComboBox;
    private ComboBox? _bodyBagComboBox;

    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;
    private bool _isLoading;

    public event EventHandler? CommentChanged;
    public event EventHandler? TagChanged;
    public event EventHandler? FlagsChanged;

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
        _factionComboBox = this.FindControl<ComboBox>("FactionComboBox");
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

        // Wire up flag checkbox events
        WireUpFlagCheckboxes();
    }

    private void WireUpFlagCheckboxes()
    {
        void WireFlag(CheckBox? cb, Action<bool> setter)
        {
            if (cb != null)
            {
                cb.IsCheckedChanged += (s, e) =>
                {
                    if (!_isLoading && _currentCreature != null)
                    {
                        setter(cb.IsChecked ?? false);
                        FlagsChanged?.Invoke(this, EventArgs.Empty);
                    }
                };
            }
        }

        WireFlag(_plotCheckBox, v => { if (_currentCreature != null) _currentCreature.Plot = v; });
        WireFlag(_immortalCheckBox, v => { if (_currentCreature != null) _currentCreature.IsImmortal = v; });
        WireFlag(_noPermDeathCheckBox, v => { if (_currentCreature != null) _currentCreature.NoPermDeath = v; });
        WireFlag(_isPCCheckBox, v => { if (_currentCreature != null) _currentCreature.IsPC = v; });
        WireFlag(_disarmableCheckBox, v => { if (_currentCreature != null) _currentCreature.Disarmable = v; });
        WireFlag(_lootableCheckBox, v => { if (_currentCreature != null) _currentCreature.Lootable = v; });
        WireFlag(_interruptableCheckBox, v => { if (_currentCreature != null) _currentCreature.Interruptable = v; });
    }

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
        LoadBehaviorData();
    }

    /// <summary>
    /// Set whether the current file is a BIC (player character) or UTC (creature blueprint).
    /// This controls visibility of UTC-only fields like Blueprint ResRef and Comment.
    /// </summary>
    public void SetFileType(bool isBicFile)
    {
        // Hide ResRef and Comment rows for BIC files (these fields don't exist in BIC)
        if (_resRefRow != null)
            _resRefRow.IsVisible = !isBicFile;
        if (_commentRow != null)
            _commentRow.IsVisible = !isBicFile;
    }

    private void LoadBehaviorData()
    {
        // Perception Range dropdown
        if (_perceptionComboBox != null)
        {
            _perceptionComboBox.Items.Clear();
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Short (9)", Tag = (byte)9 });
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Medium (10)", Tag = (byte)10 });
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Normal (11)", Tag = (byte)11 });
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Long (12)", Tag = (byte)12 });
            _perceptionComboBox.Items.Add(new ComboBoxItem { Content = "Maximum (13)", Tag = (byte)13 });
        }

        // Walk Rate dropdown
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

        // Decay Time dropdown - common presets
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

        // Body Bag dropdown - from bodybag.2da
        if (_bodyBagComboBox != null)
        {
            _bodyBagComboBox.Items.Clear();
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "Default (0)", Tag = (byte)0 });
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "Body Bag (1)", Tag = (byte)1 });
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "Pouch (2)", Tag = (byte)2 });
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "Treasure Pile (3)", Tag = (byte)3 });
            _bodyBagComboBox.Items.Add(new ComboBoxItem { Content = "No Body (4)", Tag = (byte)4 });
        }

        // Faction - will load from 2DA or repute.fac
        if (_factionComboBox != null && _displayService != null)
        {
            _factionComboBox.Items.Clear();
            var factions = _displayService.GetAllFactions();
            foreach (var (id, name) in factions)
            {
                _factionComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }
        }
    }

    public void LoadCreature(UtcFile? creature)
    {
        _isLoading = true;
        _currentCreature = creature;

        if (creature == null)
        {
            ClearPanel();
            _isLoading = false;
            return;
        }

        // Identity
        if (_templateResRefTextBox != null)
            _templateResRefTextBox.Text = creature.TemplateResRef ?? "";
        if (_tagTextBox != null)
            _tagTextBox.Text = creature.Tag ?? "";
        if (_commentTextBox != null)
            _commentTextBox.Text = creature.Comment ?? "";

        // Flags
        SetCheckBox(_plotCheckBox, creature.Plot);
        SetCheckBox(_immortalCheckBox, creature.IsImmortal);
        SetCheckBox(_noPermDeathCheckBox, creature.NoPermDeath);
        SetCheckBox(_isPCCheckBox, creature.IsPC);
        SetCheckBox(_disarmableCheckBox, creature.Disarmable);
        SetCheckBox(_lootableCheckBox, creature.Lootable);
        SetCheckBox(_interruptableCheckBox, creature.Interruptable);

        // Behavior - select in combos
        SelectComboByTag(_factionComboBox, creature.FactionID);
        SelectComboByTag(_perceptionComboBox, creature.PerceptionRange);
        SelectComboByTag(_walkRateComboBox, creature.WalkRate);
        SelectComboByTag(_decayTimeComboBox, creature.DecayTime);
        SelectComboByTag(_bodyBagComboBox, creature.BodyBag);

        _isLoading = false;
    }

    private void SelectComboByTag(ComboBox? combo, byte value)
    {
        if (combo == null) return;

        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is byte id && id == value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        combo.Items.Add(new ComboBoxItem { Content = value.ToString(), Tag = value });
        combo.SelectedIndex = combo.Items.Count - 1;
    }

    private void SelectComboByTag(ComboBox? combo, ushort value)
    {
        if (combo == null) return;

        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is ushort id && id == value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        combo.Items.Add(new ComboBoxItem { Content = value.ToString(), Tag = value });
        combo.SelectedIndex = combo.Items.Count - 1;
    }

    private void SelectComboByTag(ComboBox? combo, int value)
    {
        if (combo == null) return;

        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is int id && id == value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        combo.Items.Add(new ComboBoxItem { Content = value.ToString(), Tag = value });
        combo.SelectedIndex = combo.Items.Count - 1;
    }

    private void SelectComboByTag(ComboBox? combo, uint value)
    {
        if (combo == null) return;

        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is uint id && id == value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        combo.Items.Add(new ComboBoxItem { Content = $"{value} ms", Tag = value });
        combo.SelectedIndex = combo.Items.Count - 1;
    }

    public void ClearPanel()
    {
        // Clear identity
        if (_templateResRefTextBox != null)
            _templateResRefTextBox.Text = "";
        if (_tagTextBox != null)
            _tagTextBox.Text = "";
        if (_commentTextBox != null)
            _commentTextBox.Text = "";

        // Clear all flags
        SetCheckBox(_plotCheckBox, false);
        SetCheckBox(_immortalCheckBox, false);
        SetCheckBox(_noPermDeathCheckBox, false);
        SetCheckBox(_isPCCheckBox, false);
        SetCheckBox(_disarmableCheckBox, false);
        SetCheckBox(_lootableCheckBox, false);
        SetCheckBox(_interruptableCheckBox, true); // Default

        // Clear behavior combos
        if (_factionComboBox != null)
            _factionComboBox.SelectedIndex = -1;
        if (_perceptionComboBox != null)
            _perceptionComboBox.SelectedIndex = 2; // Normal (11)
        if (_walkRateComboBox != null)
            _walkRateComboBox.SelectedIndex = 0;
        if (_decayTimeComboBox != null)
            _decayTimeComboBox.SelectedIndex = 2; // 5 seconds default
        if (_bodyBagComboBox != null)
            _bodyBagComboBox.SelectedIndex = 0;
    }

    private async void OnCopyResRefClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var text = _templateResRefTextBox?.Text;
        if (!string.IsNullOrEmpty(text))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }
    }

    private async void OnCopyTagClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.Tag = _tagTextBox?.Text ?? "";
        TagChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCommentTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.Comment = _commentTextBox?.Text ?? "";
        CommentChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetComment() => _commentTextBox?.Text ?? "";
    public string GetTag() => _tagTextBox?.Text ?? "";

    private static void SetCheckBox(CheckBox? cb, bool value)
    {
        if (cb != null)
            cb.IsChecked = value;
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}
