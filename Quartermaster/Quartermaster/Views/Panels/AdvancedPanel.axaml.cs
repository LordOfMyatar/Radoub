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
    private TextBox? _subraceTextBox;
    private TextBox? _deityTextBox;
    private TextBox? _commentTextBox;
    private Button? _copyResRefButton;
    private Button? _copyTagButton;

    // Challenge Rating section
    private TextBlock? _challengeRatingText;
    private NumericUpDown? _crAdjustNumeric;

    // Appearance section
    private ComboBox? _appearanceComboBox;
    private ComboBox? _phenotypeComboBox;
    private ComboBox? _portraitComboBox;

    // Body parts section
    private Border? _bodyPartsSection;
    private StackPanel? _bodyPartsContent;
    private TextBlock? _bodyPartsStatusText;

    // Body part combos - central
    private ComboBox? _headComboBox;
    private ComboBox? _neckComboBox;
    private ComboBox? _torsoComboBox;
    private ComboBox? _pelvisComboBox;
    private ComboBox? _beltComboBox;
    private ComboBox? _tailComboBox;
    private ComboBox? _wingsComboBox;

    // Body part combos - limbs
    private ComboBox? _lShoulComboBox;
    private ComboBox? _rShoulComboBox;
    private ComboBox? _lBicepComboBox;
    private ComboBox? _rBicepComboBox;
    private ComboBox? _lFArmComboBox;
    private ComboBox? _rFArmComboBox;
    private ComboBox? _lHandComboBox;
    private ComboBox? _rHandComboBox;
    private ComboBox? _lThighComboBox;
    private ComboBox? _rThighComboBox;
    private ComboBox? _lShinComboBox;
    private ComboBox? _rShinComboBox;
    private ComboBox? _lFootComboBox;
    private ComboBox? _rFootComboBox;

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
    private ComboBox? _soundSetComboBox;
    private ComboBox? _decayTimeComboBox;
    private ComboBox? _bodyBagComboBox;

    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;
    private List<AppearanceInfo>? _appearances;
    private List<PhenotypeInfo>? _phenotypes;
    private bool _isLoading;

    public event EventHandler? CommentChanged;
    public event EventHandler? TagChanged;
    public event EventHandler? SubraceChanged;
    public event EventHandler? DeityChanged;
    public event EventHandler? CRAdjustChanged;
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
        _subraceTextBox = this.FindControl<TextBox>("SubraceTextBox");
        _deityTextBox = this.FindControl<TextBox>("DeityTextBox");
        _commentTextBox = this.FindControl<TextBox>("CommentTextBox");
        _copyResRefButton = this.FindControl<Button>("CopyResRefButton");
        _copyTagButton = this.FindControl<Button>("CopyTagButton");

        // Challenge Rating section
        _challengeRatingText = this.FindControl<TextBlock>("ChallengeRatingText");
        _crAdjustNumeric = this.FindControl<NumericUpDown>("CRAdjustNumeric");

        // Appearance section
        _appearanceComboBox = this.FindControl<ComboBox>("AppearanceComboBox");
        _phenotypeComboBox = this.FindControl<ComboBox>("PhenotypeComboBox");
        _portraitComboBox = this.FindControl<ComboBox>("PortraitComboBox");

        // Body parts section
        _bodyPartsSection = this.FindControl<Border>("BodyPartsSection");
        _bodyPartsContent = this.FindControl<StackPanel>("BodyPartsContent");
        _bodyPartsStatusText = this.FindControl<TextBlock>("BodyPartsStatusText");

        // Body part combos - central
        _headComboBox = this.FindControl<ComboBox>("HeadComboBox");
        _neckComboBox = this.FindControl<ComboBox>("NeckComboBox");
        _torsoComboBox = this.FindControl<ComboBox>("TorsoComboBox");
        _pelvisComboBox = this.FindControl<ComboBox>("PelvisComboBox");
        _beltComboBox = this.FindControl<ComboBox>("BeltComboBox");
        _tailComboBox = this.FindControl<ComboBox>("TailComboBox");
        _wingsComboBox = this.FindControl<ComboBox>("WingsComboBox");

        // Body part combos - limbs
        _lShoulComboBox = this.FindControl<ComboBox>("LShoulComboBox");
        _rShoulComboBox = this.FindControl<ComboBox>("RShoulComboBox");
        _lBicepComboBox = this.FindControl<ComboBox>("LBicepComboBox");
        _rBicepComboBox = this.FindControl<ComboBox>("RBicepComboBox");
        _lFArmComboBox = this.FindControl<ComboBox>("LFArmComboBox");
        _rFArmComboBox = this.FindControl<ComboBox>("RFArmComboBox");
        _lHandComboBox = this.FindControl<ComboBox>("LHandComboBox");
        _rHandComboBox = this.FindControl<ComboBox>("RHandComboBox");
        _lThighComboBox = this.FindControl<ComboBox>("LThighComboBox");
        _rThighComboBox = this.FindControl<ComboBox>("RThighComboBox");
        _lShinComboBox = this.FindControl<ComboBox>("LShinComboBox");
        _rShinComboBox = this.FindControl<ComboBox>("RShinComboBox");
        _lFootComboBox = this.FindControl<ComboBox>("LFootComboBox");
        _rFootComboBox = this.FindControl<ComboBox>("RFootComboBox");

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
        _soundSetComboBox = this.FindControl<ComboBox>("SoundSetComboBox");
        _decayTimeComboBox = this.FindControl<ComboBox>("DecayTimeComboBox");
        _bodyBagComboBox = this.FindControl<ComboBox>("BodyBagComboBox");

        // Wire up events
        if (_copyResRefButton != null)
            _copyResRefButton.Click += OnCopyResRefClick;
        if (_copyTagButton != null)
            _copyTagButton.Click += OnCopyTagClick;
        if (_tagTextBox != null)
            _tagTextBox.TextChanged += OnTagTextChanged;
        if (_subraceTextBox != null)
            _subraceTextBox.TextChanged += OnSubraceTextChanged;
        if (_deityTextBox != null)
            _deityTextBox.TextChanged += OnDeityTextChanged;
        if (_commentTextBox != null)
            _commentTextBox.TextChanged += OnCommentTextChanged;
        if (_crAdjustNumeric != null)
            _crAdjustNumeric.ValueChanged += OnCRAdjustValueChanged;
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectionChanged += OnAppearanceSelectionChanged;

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
        LoadAppearanceData();
        LoadBodyPartData();
        LoadBehaviorData();
    }

    private void LoadAppearanceData()
    {
        if (_displayService == null) return;

        _isLoading = true;

        // Load appearances from 2DA
        _appearances = _displayService.GetAllAppearances();
        if (_appearanceComboBox != null)
        {
            _appearanceComboBox.Items.Clear();
            foreach (var app in _appearances)
            {
                var displayText = app.IsPartBased
                    ? $"(Dynamic) {app.Name}"
                    : app.Name;
                _appearanceComboBox.Items.Add(new ComboBoxItem
                {
                    Content = displayText,
                    Tag = app.AppearanceId
                });
            }
        }

        // Load phenotypes from 2DA
        _phenotypes = _displayService.GetAllPhenotypes();
        if (_phenotypeComboBox != null)
        {
            _phenotypeComboBox.Items.Clear();
            foreach (var pheno in _phenotypes)
            {
                _phenotypeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = pheno.Name,
                    Tag = pheno.PhenotypeId
                });
            }
        }

        // Load portraits from 2DA
        LoadPortraitData();

        _isLoading = false;
    }

    private void LoadPortraitData()
    {
        if (_displayService == null || _portraitComboBox == null) return;

        _portraitComboBox.Items.Clear();
        var portraits = _displayService.GetAllPortraits();
        foreach (var (id, name) in portraits)
        {
            _portraitComboBox.Items.Add(new ComboBoxItem
            {
                Content = name,
                Tag = id
            });
        }
    }

    private void LoadBodyPartData()
    {
        if (_displayService == null) return;

        // For now, populate with numeric values 0-20
        // TODO: Load from model_*.2da files when available
        void PopulateBodyPartCombo(ComboBox? combo, int max = 20)
        {
            if (combo == null) return;
            combo.Items.Clear();
            for (int i = 0; i <= max; i++)
            {
                combo.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = (byte)i });
            }
        }

        PopulateBodyPartCombo(_headComboBox, 30);
        PopulateBodyPartCombo(_neckComboBox);
        PopulateBodyPartCombo(_torsoComboBox);
        PopulateBodyPartCombo(_pelvisComboBox);
        PopulateBodyPartCombo(_beltComboBox);

        // Tail/Wings from 2DA
        LoadTailWingsData();

        // Limbs
        PopulateBodyPartCombo(_lShoulComboBox);
        PopulateBodyPartCombo(_rShoulComboBox);
        PopulateBodyPartCombo(_lBicepComboBox);
        PopulateBodyPartCombo(_rBicepComboBox);
        PopulateBodyPartCombo(_lFArmComboBox);
        PopulateBodyPartCombo(_rFArmComboBox);
        PopulateBodyPartCombo(_lHandComboBox);
        PopulateBodyPartCombo(_rHandComboBox);
        PopulateBodyPartCombo(_lThighComboBox);
        PopulateBodyPartCombo(_rThighComboBox);
        PopulateBodyPartCombo(_lShinComboBox);
        PopulateBodyPartCombo(_rShinComboBox);
        PopulateBodyPartCombo(_lFootComboBox);
        PopulateBodyPartCombo(_rFootComboBox);
    }

    private void LoadTailWingsData()
    {
        if (_displayService == null) return;

        // Load tails
        if (_tailComboBox != null)
        {
            _tailComboBox.Items.Clear();
            var tails = _displayService.GetAllTails();
            foreach (var (id, name) in tails)
            {
                _tailComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }
        }

        // Load wings
        if (_wingsComboBox != null)
        {
            _wingsComboBox.Items.Clear();
            var wings = _displayService.GetAllWings();
            foreach (var (id, name) in wings)
            {
                _wingsComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }
        }
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

        // Sound Set - will load from 2DA
        if (_soundSetComboBox != null && _displayService != null)
        {
            _soundSetComboBox.Items.Clear();
            var soundSets = _displayService.GetAllSoundSets();
            foreach (var (id, name) in soundSets)
            {
                _soundSetComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }
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
        if (_subraceTextBox != null)
            _subraceTextBox.Text = creature.Subrace ?? "";
        if (_deityTextBox != null)
            _deityTextBox.Text = creature.Deity ?? "";
        if (_commentTextBox != null)
            _commentTextBox.Text = creature.Comment ?? "";

        // Challenge Rating
        if (_challengeRatingText != null)
            _challengeRatingText.Text = creature.ChallengeRating.ToString("F1");
        if (_crAdjustNumeric != null)
            _crAdjustNumeric.Value = creature.CRAdjust;

        // Appearance - select in combo
        SelectAppearance(creature.AppearanceType);
        SelectPhenotype(creature.Phenotype);
        SelectPortrait(creature.PortraitId);

        // Body parts - update enabled state and values
        var isPartBased = _displayService?.IsPartBasedAppearance(creature.AppearanceType) ?? false;
        UpdateBodyPartsEnabledState(isPartBased);
        LoadBodyPartValues(creature);

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
        SelectComboByTag(_soundSetComboBox, creature.SoundSetFile);
        SelectComboByTag(_decayTimeComboBox, creature.DecayTime);
        SelectComboByTag(_bodyBagComboBox, creature.BodyBag);

        _isLoading = false;
    }

    private void UpdateBodyPartsEnabledState(bool isPartBased)
    {
        if (_bodyPartsContent != null)
            _bodyPartsContent.IsEnabled = isPartBased;

        if (_bodyPartsStatusText != null)
        {
            _bodyPartsStatusText.Text = isPartBased
                ? "(Dynamic Appearance)"
                : "(Static Appearance - body parts not editable)";
        }

        // Set opacity for visual feedback
        if (_bodyPartsContent != null)
            _bodyPartsContent.Opacity = isPartBased ? 1.0 : 0.5;
    }

    private void LoadBodyPartValues(UtcFile creature)
    {
        SelectComboByTag(_headComboBox, creature.AppearanceHead);
        SelectComboByTag(_neckComboBox, creature.BodyPart_Neck);
        SelectComboByTag(_torsoComboBox, creature.BodyPart_Torso);
        SelectComboByTag(_pelvisComboBox, creature.BodyPart_Pelvis);
        SelectComboByTag(_beltComboBox, creature.BodyPart_Belt);
        SelectComboByTag(_tailComboBox, creature.Tail);
        SelectComboByTag(_wingsComboBox, creature.Wings);

        SelectComboByTag(_lShoulComboBox, creature.BodyPart_LShoul);
        SelectComboByTag(_rShoulComboBox, creature.BodyPart_RShoul);
        SelectComboByTag(_lBicepComboBox, creature.BodyPart_LBicep);
        SelectComboByTag(_rBicepComboBox, creature.BodyPart_RBicep);
        SelectComboByTag(_lFArmComboBox, creature.BodyPart_LFArm);
        SelectComboByTag(_rFArmComboBox, creature.BodyPart_RFArm);
        SelectComboByTag(_lHandComboBox, creature.BodyPart_LHand);
        SelectComboByTag(_rHandComboBox, creature.BodyPart_RHand);
        SelectComboByTag(_lThighComboBox, creature.BodyPart_LThigh);
        SelectComboByTag(_rThighComboBox, creature.BodyPart_RThigh);
        SelectComboByTag(_lShinComboBox, creature.BodyPart_LShin);
        SelectComboByTag(_rShinComboBox, creature.BodyPart_RShin);
        SelectComboByTag(_lFootComboBox, creature.BodyPart_LFoot);
        SelectComboByTag(_rFootComboBox, creature.BodyPart_RFoot);
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

    private void SelectAppearance(ushort appearanceId)
    {
        if (_appearanceComboBox == null || _appearances == null) return;

        for (int i = 0; i < _appearanceComboBox.Items.Count; i++)
        {
            if (_appearanceComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is ushort id && id == appearanceId)
            {
                _appearanceComboBox.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        _appearanceComboBox.Items.Add(new ComboBoxItem
        {
            Content = $"Appearance {appearanceId}",
            Tag = appearanceId
        });
        _appearanceComboBox.SelectedIndex = _appearanceComboBox.Items.Count - 1;
    }

    private void SelectPhenotype(int phenotypeId)
    {
        if (_phenotypeComboBox == null || _phenotypes == null) return;

        for (int i = 0; i < _phenotypeComboBox.Items.Count; i++)
        {
            if (_phenotypeComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is int id && id == phenotypeId)
            {
                _phenotypeComboBox.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        _phenotypeComboBox.Items.Add(new ComboBoxItem
        {
            Content = $"Phenotype {phenotypeId}",
            Tag = phenotypeId
        });
        _phenotypeComboBox.SelectedIndex = _phenotypeComboBox.Items.Count - 1;
    }

    private void SelectPortrait(ushort portraitId)
    {
        if (_portraitComboBox == null) return;

        for (int i = 0; i < _portraitComboBox.Items.Count; i++)
        {
            if (_portraitComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is ushort id && id == portraitId)
            {
                _portraitComboBox.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        var name = _displayService?.GetPortraitName(portraitId) ?? $"Portrait {portraitId}";
        _portraitComboBox.Items.Add(new ComboBoxItem
        {
            Content = name,
            Tag = portraitId
        });
        _portraitComboBox.SelectedIndex = _portraitComboBox.Items.Count - 1;
    }

    private void OnAppearanceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _appearanceComboBox?.SelectedItem is not ComboBoxItem item) return;

        if (item.Tag is ushort appearanceId)
        {
            var isPartBased = _displayService?.IsPartBasedAppearance(appearanceId) ?? false;
            UpdateBodyPartsEnabledState(isPartBased);
        }
    }

    public void ClearPanel()
    {
        // Clear identity
        if (_templateResRefTextBox != null)
            _templateResRefTextBox.Text = "";
        if (_tagTextBox != null)
            _tagTextBox.Text = "";
        if (_subraceTextBox != null)
            _subraceTextBox.Text = "";
        if (_deityTextBox != null)
            _deityTextBox.Text = "";
        if (_commentTextBox != null)
            _commentTextBox.Text = "";

        // Clear Challenge Rating
        if (_challengeRatingText != null)
            _challengeRatingText.Text = "0.0";
        if (_crAdjustNumeric != null)
            _crAdjustNumeric.Value = 0;

        // Clear appearance
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectedIndex = -1;
        if (_phenotypeComboBox != null)
            _phenotypeComboBox.SelectedIndex = -1;
        if (_portraitComboBox != null)
            _portraitComboBox.SelectedIndex = -1;

        // Disable body parts section
        UpdateBodyPartsEnabledState(false);

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
        if (_soundSetComboBox != null)
            _soundSetComboBox.SelectedIndex = -1;
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

    private void OnSubraceTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.Subrace = _subraceTextBox?.Text ?? "";
        SubraceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeityTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.Deity = _deityTextBox?.Text ?? "";
        DeityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCRAdjustValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.CRAdjust = (int)(e.NewValue ?? 0);
        CRAdjustChanged?.Invoke(this, EventArgs.Empty);
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
