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
    private TextBlock? _templateResRefText;
    private TextBlock? _tagText;
    private TextBox? _commentTextBox;
    private Button? _copyResRefButton;
    private Button? _copyTagButton;

    // Appearance section
    private ComboBox? _appearanceComboBox;
    private ComboBox? _phenotypeComboBox;
    private TextBlock? _portraitText;
    private TextBlock? _portraitIdText;
    private TextBlock? _tailText;
    private TextBlock? _wingsText;

    // Body parts section
    private Border? _bodyPartsSection;
    private TextBlock? _headText;
    private TextBlock? _neckText;
    private TextBlock? _torsoText;
    private TextBlock? _pelvisText;
    private TextBlock? _beltText;
    private TextBlock? _lShoulText;
    private TextBlock? _rShoulText;
    private TextBlock? _lBicepText;
    private TextBlock? _rBicepText;
    private TextBlock? _lFArmText;
    private TextBlock? _rFArmText;
    private TextBlock? _lHandText;
    private TextBlock? _rHandText;
    private TextBlock? _lThighText;
    private TextBlock? _rThighText;
    private TextBlock? _lShinText;
    private TextBlock? _rShinText;
    private TextBlock? _lFootText;
    private TextBlock? _rFootText;

    // Flag icons
    private TextBlock? _plotIcon;
    private TextBlock? _immortalIcon;
    private TextBlock? _noPermDeathIcon;
    private TextBlock? _isPCIcon;
    private TextBlock? _disarmableIcon;
    private TextBlock? _lootableIcon;
    private TextBlock? _interruptableIcon;

    // Behavior
    private TextBlock? _factionText;
    private TextBlock? _perceptionText;
    private TextBlock? _walkRateText;
    private TextBlock? _soundSetText;
    private TextBlock? _decayTimeText;
    private TextBlock? _bodyBagText;

    private const string CheckedIcon = "[X]";
    private const string UncheckedIcon = "[  ]";

    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;
    private List<AppearanceInfo>? _appearances;
    private List<PhenotypeInfo>? _phenotypes;
    private bool _isLoading;

    public event EventHandler? CommentChanged;

    public AdvancedPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Identity section
        _templateResRefText = this.FindControl<TextBlock>("TemplateResRefText");
        _tagText = this.FindControl<TextBlock>("TagText");
        _commentTextBox = this.FindControl<TextBox>("CommentTextBox");
        _copyResRefButton = this.FindControl<Button>("CopyResRefButton");
        _copyTagButton = this.FindControl<Button>("CopyTagButton");

        // Appearance section
        _appearanceComboBox = this.FindControl<ComboBox>("AppearanceComboBox");
        _phenotypeComboBox = this.FindControl<ComboBox>("PhenotypeComboBox");
        _portraitText = this.FindControl<TextBlock>("PortraitText");
        _portraitIdText = this.FindControl<TextBlock>("PortraitIdText");
        _tailText = this.FindControl<TextBlock>("TailText");
        _wingsText = this.FindControl<TextBlock>("WingsText");

        // Body parts section
        _bodyPartsSection = this.FindControl<Border>("BodyPartsSection");
        _headText = this.FindControl<TextBlock>("HeadText");
        _neckText = this.FindControl<TextBlock>("NeckText");
        _torsoText = this.FindControl<TextBlock>("TorsoText");
        _pelvisText = this.FindControl<TextBlock>("PelvisText");
        _beltText = this.FindControl<TextBlock>("BeltText");
        _lShoulText = this.FindControl<TextBlock>("LShoulText");
        _rShoulText = this.FindControl<TextBlock>("RShoulText");
        _lBicepText = this.FindControl<TextBlock>("LBicepText");
        _rBicepText = this.FindControl<TextBlock>("RBicepText");
        _lFArmText = this.FindControl<TextBlock>("LFArmText");
        _rFArmText = this.FindControl<TextBlock>("RFArmText");
        _lHandText = this.FindControl<TextBlock>("LHandText");
        _rHandText = this.FindControl<TextBlock>("RHandText");
        _lThighText = this.FindControl<TextBlock>("LThighText");
        _rThighText = this.FindControl<TextBlock>("RThighText");
        _lShinText = this.FindControl<TextBlock>("LShinText");
        _rShinText = this.FindControl<TextBlock>("RShinText");
        _lFootText = this.FindControl<TextBlock>("LFootText");
        _rFootText = this.FindControl<TextBlock>("RFootText");

        // Flag icons
        _plotIcon = this.FindControl<TextBlock>("PlotIcon");
        _immortalIcon = this.FindControl<TextBlock>("ImmortalIcon");
        _noPermDeathIcon = this.FindControl<TextBlock>("NoPermDeathIcon");
        _isPCIcon = this.FindControl<TextBlock>("IsPCIcon");
        _disarmableIcon = this.FindControl<TextBlock>("DisarmableIcon");
        _lootableIcon = this.FindControl<TextBlock>("LootableIcon");
        _interruptableIcon = this.FindControl<TextBlock>("InterruptableIcon");

        // Behavior
        _factionText = this.FindControl<TextBlock>("FactionText");
        _perceptionText = this.FindControl<TextBlock>("PerceptionText");
        _walkRateText = this.FindControl<TextBlock>("WalkRateText");
        _soundSetText = this.FindControl<TextBlock>("SoundSetText");
        _decayTimeText = this.FindControl<TextBlock>("DecayTimeText");
        _bodyBagText = this.FindControl<TextBlock>("BodyBagText");

        // Wire up events
        if (_copyResRefButton != null)
            _copyResRefButton.Click += OnCopyResRefClick;
        if (_copyTagButton != null)
            _copyTagButton.Click += OnCopyTagClick;
        if (_commentTextBox != null)
            _commentTextBox.TextChanged += OnCommentTextChanged;
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectionChanged += OnAppearanceSelectionChanged;
    }

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
        LoadAppearanceData();
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

        _isLoading = false;
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
        SetText(_templateResRefText, string.IsNullOrEmpty(creature.TemplateResRef) ? "-" : creature.TemplateResRef);
        SetText(_tagText, string.IsNullOrEmpty(creature.Tag) ? "-" : creature.Tag);
        if (_commentTextBox != null)
            _commentTextBox.Text = creature.Comment ?? "";

        // Appearance - select in combo
        SelectAppearance(creature.AppearanceType);
        SelectPhenotype(creature.Phenotype);

        // Portrait
        if (_displayService != null)
        {
            var portraitResRef = _displayService.GetPortraitResRef(creature.PortraitId);
            SetText(_portraitText, portraitResRef ?? $"Portrait {creature.PortraitId}");
            SetText(_portraitIdText, $"(ID: {creature.PortraitId})");
            SetText(_tailText, _displayService.GetTailName(creature.Tail));
            SetText(_wingsText, _displayService.GetWingName(creature.Wings));
        }
        else
        {
            SetText(_portraitText, $"Portrait {creature.PortraitId}");
            SetText(_portraitIdText, $"(ID: {creature.PortraitId})");
            SetText(_tailText, creature.Tail == 0 ? "None" : creature.Tail.ToString());
            SetText(_wingsText, creature.Wings == 0 ? "None" : creature.Wings.ToString());
        }

        // Body parts (show section if part-based)
        var isPartBased = _displayService?.IsPartBasedAppearance(creature.AppearanceType) ?? false;
        if (_bodyPartsSection != null)
            _bodyPartsSection.IsVisible = isPartBased;

        if (isPartBased)
        {
            LoadBodyParts(creature);
        }

        // Flags
        SetFlag(_plotIcon, creature.Plot);
        SetFlag(_immortalIcon, creature.IsImmortal);
        SetFlag(_noPermDeathIcon, creature.NoPermDeath);
        SetFlag(_isPCIcon, creature.IsPC);
        SetFlag(_disarmableIcon, creature.Disarmable);
        SetFlag(_lootableIcon, creature.Lootable);
        SetFlag(_interruptableIcon, creature.Interruptable);

        // Behavior
        SetText(_factionText, creature.FactionID.ToString());
        SetText(_perceptionText, GetPerceptionRangeName(creature.PerceptionRange));
        SetText(_walkRateText, GetWalkRateName(creature.WalkRate));
        SetText(_soundSetText, creature.SoundSetFile.ToString());
        SetText(_decayTimeText, $"{creature.DecayTime} ms");
        SetText(_bodyBagText, creature.BodyBag.ToString());

        _isLoading = false;
    }

    private void LoadBodyParts(UtcFile creature)
    {
        SetText(_headText, creature.AppearanceHead.ToString());
        SetText(_neckText, creature.BodyPart_Neck.ToString());
        SetText(_torsoText, creature.BodyPart_Torso.ToString());
        SetText(_pelvisText, creature.BodyPart_Pelvis.ToString());
        SetText(_beltText, creature.BodyPart_Belt.ToString());
        SetText(_lShoulText, creature.BodyPart_LShoul.ToString());
        SetText(_rShoulText, creature.BodyPart_RShoul.ToString());
        SetText(_lBicepText, creature.BodyPart_LBicep.ToString());
        SetText(_rBicepText, creature.BodyPart_RBicep.ToString());
        SetText(_lFArmText, creature.BodyPart_LFArm.ToString());
        SetText(_rFArmText, creature.BodyPart_RFArm.ToString());
        SetText(_lHandText, creature.BodyPart_LHand.ToString());
        SetText(_rHandText, creature.BodyPart_RHand.ToString());
        SetText(_lThighText, creature.BodyPart_LThigh.ToString());
        SetText(_rThighText, creature.BodyPart_RThigh.ToString());
        SetText(_lShinText, creature.BodyPart_LShin.ToString());
        SetText(_rShinText, creature.BodyPart_RShin.ToString());
        SetText(_lFootText, creature.BodyPart_LFoot.ToString());
        SetText(_rFootText, creature.BodyPart_RFoot.ToString());
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

    private void OnAppearanceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _appearanceComboBox?.SelectedItem is not ComboBoxItem item) return;

        if (item.Tag is ushort appearanceId)
        {
            var isPartBased = _displayService?.IsPartBasedAppearance(appearanceId) ?? false;
            if (_bodyPartsSection != null)
                _bodyPartsSection.IsVisible = isPartBased;
        }
    }

    public void ClearPanel()
    {
        // Clear identity
        SetText(_templateResRefText, "-");
        SetText(_tagText, "-");
        if (_commentTextBox != null)
            _commentTextBox.Text = "";

        // Clear appearance
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectedIndex = -1;
        if (_phenotypeComboBox != null)
            _phenotypeComboBox.SelectedIndex = -1;
        SetText(_portraitText, "-");
        SetText(_portraitIdText, "");
        SetText(_tailText, "None");
        SetText(_wingsText, "None");

        // Hide body parts section
        if (_bodyPartsSection != null)
            _bodyPartsSection.IsVisible = false;

        // Clear all flags
        SetFlag(_plotIcon, false);
        SetFlag(_immortalIcon, false);
        SetFlag(_noPermDeathIcon, false);
        SetFlag(_isPCIcon, false);
        SetFlag(_disarmableIcon, false);
        SetFlag(_lootableIcon, false);
        SetFlag(_interruptableIcon, true); // Default

        // Clear behavior
        SetText(_factionText, "0");
        SetText(_perceptionText, "Normal");
        SetText(_walkRateText, "Normal");
        SetText(_soundSetText, "0");
        SetText(_decayTimeText, "5000 ms");
        SetText(_bodyBagText, "0");
    }

    private async void OnCopyResRefClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var text = _templateResRefText?.Text;
        if (!string.IsNullOrEmpty(text) && text != "-")
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }
    }

    private async void OnCopyTagClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var text = _tagText?.Text;
        if (!string.IsNullOrEmpty(text) && text != "-")
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(text);
        }
    }

    private void OnCommentTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.Comment = _commentTextBox?.Text ?? "";
        CommentChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetComment() => _commentTextBox?.Text ?? "";

    private static string GetPerceptionRangeName(byte range)
    {
        return range switch
        {
            9 => "Short",
            10 => "Medium",
            11 => "Normal",
            12 => "Long",
            13 => "Maximum",
            _ => $"{range}"
        };
    }

    private static string GetWalkRateName(int rate)
    {
        return rate switch
        {
            0 => "PC",
            1 => "Immobile",
            2 => "Very Slow",
            3 => "Slow",
            4 => "Normal",
            5 => "Fast",
            6 => "Very Fast",
            7 => "Default",
            _ => $"{rate}"
        };
    }

    private void SetFlag(TextBlock? icon, bool value)
    {
        if (icon != null)
            icon.Text = value ? CheckedIcon : UncheckedIcon;
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}
