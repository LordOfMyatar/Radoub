using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

public partial class AppearancePanel : UserControl
{
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

    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;
    private List<AppearanceInfo>? _appearances;
    private List<PhenotypeInfo>? _phenotypes;
    private bool _isLoading;

    public event EventHandler? AppearanceChanged;

    public AppearancePanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

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

        // Wire up events
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectionChanged += OnAppearanceSelectionChanged;
    }

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
        LoadAppearanceData();
        LoadBodyPartData();
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

        // Appearance - select in combo
        SelectAppearance(creature.AppearanceType);
        SelectPhenotype(creature.Phenotype);
        SelectPortrait(creature.PortraitId);

        // Body parts - update enabled state and values
        var isPartBased = _displayService?.IsPartBasedAppearance(creature.AppearanceType) ?? false;
        UpdateBodyPartsEnabledState(isPartBased);
        LoadBodyPartValues(creature);

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
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearPanel()
    {
        // Clear appearance
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectedIndex = -1;
        if (_phenotypeComboBox != null)
            _phenotypeComboBox.SelectedIndex = -1;
        if (_portraitComboBox != null)
            _portraitComboBox.SelectedIndex = -1;

        // Disable body parts section
        UpdateBodyPartsEnabledState(false);
    }
}
