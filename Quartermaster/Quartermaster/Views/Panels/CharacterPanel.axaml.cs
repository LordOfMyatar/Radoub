using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Bic;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Ssf;
using Radoub.Formats.Utc;
using Radoub.UI.Controls;
using Radoub.UI.Services;

namespace Quartermaster.Views.Panels;

/// <summary>
/// Character identity panel: name, race, portrait, soundset, conversation, BIC fields.
/// Partial classes: Soundset (preview playback), Dialogs (browse dialog handlers).
/// </summary>
public partial class CharacterPanel : UserControl
{
    private SpellCheckTextBox? _firstNameTextBox;
    private SpellCheckTextBox? _lastNameTextBox;
    private ComboBox? _raceComboBox;
    private TextBox? _subraceTextBox;
    private TextBox? _deityTextBox;
    private ComboBox? _portraitComboBox;
    private ComboBox? _soundSetComboBox;
    private TextBox? _conversationTextBox;
    private Button? _browseConversationButton;
    private Button? _clearConversationButton;
    private Button? _browsePortraitButton;
    private Button? _browseSoundSetButton;

    // Portrait ID/ResRef fields for debugging Aurora Toolset portrait issues
    private TextBox? _portraitIdTextBox;
    private TextBox? _portraitResRefTextBox;
    private TextBlock? _portraitIdLabel;
    private TextBlock? _portraitResRefLabel;

    // Conversation row visibility controls
    private TextBlock? _conversationLabel;
    private StackPanel? _conversationRow;

    // Soundset preview controls (#916)
    private ComboBox? _soundsetTypeComboBox;
    private Button? _soundsetPlayButton;
    private AudioService? _audioService;
    private List<SoundsetTypeItem> _soundsetTypeItems = new();

    // BIC-specific controls
    private Border? _playerCharacterSection;
    private Border? _biographySection;
    private TextBox? _experienceTextBox;
    private TextBox? _goldTextBox;
    private TextBox? _ageTextBox;
    private SpellCheckTextBox? _biographyTextBox;
    private readonly QuickTokenService _quickTokenService = new();

    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;
    private bool _isLoading;
    private bool _isBicFile;
    private string? _currentFilePath;
    private IGameDataService? _gameDataService;
    private ItemIconService? _itemIconService;

    public event EventHandler? CharacterChanged;
    public event EventHandler? PortraitChanged;

    public CharacterPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _firstNameTextBox = this.FindControl<SpellCheckTextBox>("FirstNameTextBox");
        _lastNameTextBox = this.FindControl<SpellCheckTextBox>("LastNameTextBox");
        _raceComboBox = this.FindControl<ComboBox>("RaceComboBox");
        _subraceTextBox = this.FindControl<TextBox>("SubraceTextBox");
        _deityTextBox = this.FindControl<TextBox>("DeityTextBox");
        _portraitComboBox = this.FindControl<ComboBox>("PortraitComboBox");
        _soundSetComboBox = this.FindControl<ComboBox>("SoundSetComboBox");
        _conversationTextBox = this.FindControl<TextBox>("ConversationTextBox");
        _browseConversationButton = this.FindControl<Button>("BrowseConversationButton");
        _clearConversationButton = this.FindControl<Button>("ClearConversationButton");
        _browsePortraitButton = this.FindControl<Button>("BrowsePortraitButton");
        _browseSoundSetButton = this.FindControl<Button>("BrowseSoundSetButton");

        // Portrait ID/ResRef fields
        _portraitIdTextBox = this.FindControl<TextBox>("PortraitIdTextBox");
        _portraitResRefTextBox = this.FindControl<TextBox>("PortraitResRefTextBox");
        _portraitIdLabel = this.FindControl<TextBlock>("PortraitIdLabel");
        _portraitResRefLabel = this.FindControl<TextBlock>("PortraitResRefLabel");

        // Conversation row visibility controls
        _conversationLabel = this.FindControl<TextBlock>("ConversationLabel");
        _conversationRow = this.FindControl<StackPanel>("ConversationRow");

        // Soundset preview controls (#916)
        _soundsetTypeComboBox = this.FindControl<ComboBox>("SoundsetTypeComboBox");
        _soundsetPlayButton = this.FindControl<Button>("SoundsetPlayButton");

        // BIC-specific controls
        _playerCharacterSection = this.FindControl<Border>("PlayerCharacterSection");
        _biographySection = this.FindControl<Border>("BiographySection");
        _experienceTextBox = this.FindControl<TextBox>("ExperienceTextBox");
        _goldTextBox = this.FindControl<TextBox>("GoldTextBox");
        _ageTextBox = this.FindControl<TextBox>("AgeTextBox");
        _biographyTextBox = this.FindControl<SpellCheckTextBox>("BiographyTextBox");

        // Wire up token insertion context menu (#1817)
        WireTokenMenu(_firstNameTextBox);
        WireTokenMenu(_lastNameTextBox);
        WireTokenMenu(_biographyTextBox);

        // Wire up events - common fields
        if (_firstNameTextBox != null)
            _firstNameTextBox.TextChanged += OnTextChanged;
        if (_lastNameTextBox != null)
            _lastNameTextBox.TextChanged += OnTextChanged;
        if (_subraceTextBox != null)
            _subraceTextBox.TextChanged += OnTextChanged;
        if (_deityTextBox != null)
            _deityTextBox.TextChanged += OnTextChanged;
        if (_raceComboBox != null)
            _raceComboBox.SelectionChanged += OnSelectionChanged;
        if (_portraitComboBox != null)
            _portraitComboBox.SelectionChanged += OnPortraitSelectionChanged;
        if (_soundSetComboBox != null)
            _soundSetComboBox.SelectionChanged += OnSelectionChanged;
        if (_conversationTextBox != null)
            _conversationTextBox.TextChanged += OnTextChanged;
        if (_browseConversationButton != null)
            _browseConversationButton.Click += OnBrowseConversationClick;
        if (_clearConversationButton != null)
            _clearConversationButton.Click += OnClearConversationClick;
        if (_browsePortraitButton != null)
            _browsePortraitButton.Click += OnBrowsePortraitClick;
        if (_browseSoundSetButton != null)
            _browseSoundSetButton.Click += OnBrowseSoundSetClick;

        // Portrait ID/ResRef fields
        if (_portraitIdTextBox != null)
            _portraitIdTextBox.TextChanged += OnPortraitIdTextChanged;
        if (_portraitResRefTextBox != null)
            _portraitResRefTextBox.TextChanged += OnPortraitResRefTextChanged;

        // Wire up soundset preview (#916)
        if (_soundsetPlayButton != null)
            _soundsetPlayButton.Click += OnSoundsetPlayClick;
        if (_soundsetTypeComboBox != null)
            InitializeSoundsetTypeComboBox();

        // Wire up events - BIC-specific fields
        if (_experienceTextBox != null)
            _experienceTextBox.TextChanged += OnBicFieldChanged;
        if (_goldTextBox != null)
            _goldTextBox.TextChanged += OnBicFieldChanged;
        if (_ageTextBox != null)
            _ageTextBox.TextChanged += OnBicFieldChanged;
        if (_biographyTextBox != null)
            _biographyTextBox.TextChanged += OnTextChanged;
    }

    #region Token Insertion

    private void WireTokenMenu(SpellCheckTextBox? textBox)
    {
        if (textBox == null) return;
        textBox.ContextMenuExtras = menu =>
            TokenContextMenu.AppendTokenMenu(menu, textBox, () =>
                TokenInsertionHelper.OpenTokenWindow(textBox, this.VisualRoot as Window),
                _quickTokenService);
    }

    public bool HandleInsertToken()
    {
        SpellCheckTextBox? target = null;
        if (_firstNameTextBox?.IsFocused == true) target = _firstNameTextBox;
        else if (_lastNameTextBox?.IsFocused == true) target = _lastNameTextBox;
        else if (_biographyTextBox?.IsFocused == true) target = _biographyTextBox;

        if (target != null)
        {
            TokenInsertionHelper.OpenTokenWindow(target, this.VisualRoot as Window);
            return true;
        }
        return false;
    }

    #endregion

    #region Service Setup

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
        LoadRaceData();
        LoadPortraitData();
        LoadSoundSetData();
    }

    public void SetGameDataService(IGameDataService? gameDataService)
    {
        _gameDataService = gameDataService;
    }

    public void SetItemIconService(ItemIconService? itemIconService)
    {
        _itemIconService = itemIconService;
    }

    public void SetCurrentFilePath(string? filePath)
    {
        _currentFilePath = filePath;
    }

    #endregion

    #region File Type

    /// <summary>
    /// Set whether the current file is a BIC (player character) or UTC (creature blueprint).
    /// This controls visibility of BIC-specific fields and hides conversation for BIC files.
    /// </summary>
    public void SetFileType(bool isBicFile)
    {
        _isBicFile = isBicFile;
        UpdateFileTypeVisibility();
    }

    private void UpdateFileTypeVisibility()
    {
        // Hide conversation row for BIC files (player characters don't have assigned conversations)
        if (_conversationLabel != null)
            _conversationLabel.IsVisible = !_isBicFile;
        if (_conversationRow != null)
            _conversationRow.IsVisible = !_isBicFile;

        // Show BIC-specific sections for BIC files
        if (_playerCharacterSection != null)
            _playerCharacterSection.IsVisible = _isBicFile;
        if (_biographySection != null)
            _biographySection.IsVisible = _isBicFile;

        // ============================================================
        // PORTRAIT FIELD ENABLE/DISABLE LOGIC
        // ============================================================
        // BIC files: Use Portrait string (e.g., "po_hu_m_01_"), PortraitId is typically 0
        // UTC files: Use PortraitId (row in portraits.2da), Portrait string may be empty
        //
        // We enable both fields but gray out the one that's not primary for the file type
        // This allows users to see and edit both values when needed (e.g., fixing toolset errors)
        if (_portraitIdTextBox != null)
        {
            // PortraitId is primary for UTC files
            _portraitIdTextBox.IsEnabled = !_isBicFile;
            _portraitIdTextBox.Opacity = _isBicFile ? 0.5 : 1.0;
        }
        if (_portraitIdLabel != null)
            _portraitIdLabel.Opacity = _isBicFile ? 0.5 : 1.0;

        if (_portraitResRefTextBox != null)
        {
            // Portrait string is primary for BIC files
            _portraitResRefTextBox.IsEnabled = _isBicFile;
            _portraitResRefTextBox.Opacity = _isBicFile ? 1.0 : 0.5;
        }
        if (_portraitResRefLabel != null)
            _portraitResRefLabel.Opacity = _isBicFile ? 1.0 : 0.5;
    }

    #endregion

    #region Data Loading

    private void LoadRaceData()
    {
        if (_displayService == null || _raceComboBox == null) return;

        _raceComboBox.Items.Clear();
        var races = _displayService.GetAllRaces();
        foreach (var (id, name) in races)
        {
            _raceComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }
    }

    private void LoadPortraitData()
    {
        if (_displayService == null || _portraitComboBox == null) return;

        _portraitComboBox.Items.Clear();
        var portraits = _displayService.GetAllPortraits();
        foreach (var (id, name) in portraits)
        {
            _portraitComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }
    }

    private void LoadSoundSetData()
    {
        if (_displayService == null || _soundSetComboBox == null) return;

        _soundSetComboBox.Items.Clear();
        var soundSets = _displayService.GetAllSoundSets();
        foreach (var (id, name) in soundSets)
        {
            _soundSetComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
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

        // Name
        if (_firstNameTextBox != null)
            _firstNameTextBox.Text = creature.FirstName?.GetString(0) ?? "";
        if (_lastNameTextBox != null)
            _lastNameTextBox.Text = creature.LastName?.GetString(0) ?? "";

        // File and Metadata
        SelectRace(creature.Race);
        if (_subraceTextBox != null)
            _subraceTextBox.Text = creature.Subrace ?? "";
        if (_deityTextBox != null)
            _deityTextBox.Text = creature.Deity ?? "";

        // Portrait: Use PortraitId if set, otherwise look up Portrait string in portraits.2da
        // BIC files often use the Portrait string field when PortraitId is 0
        var portraitId = creature.PortraitId;
        if (portraitId == 0 && !string.IsNullOrEmpty(creature.Portrait))
        {
            var foundId = _displayService?.FindPortraitIdByResRef(creature.Portrait);
            if (foundId.HasValue)
            {
                portraitId = foundId.Value;
            }
        }
        SelectPortrait(portraitId, creature.Portrait);

        // Portrait ID and ResRef fields (show raw values for debugging)
        if (_portraitIdTextBox != null)
            _portraitIdTextBox.Text = creature.PortraitId.ToString();
        if (_portraitResRefTextBox != null)
            _portraitResRefTextBox.Text = creature.Portrait ?? "";

        // Voice & Dialog
        SelectSoundSet(creature.SoundSetFile);
        if (_conversationTextBox != null)
            _conversationTextBox.Text = creature.Conversation ?? "";

        // BIC-specific fields
        if (creature is BicFile bicFile)
        {
            if (_experienceTextBox != null)
                _experienceTextBox.Text = bicFile.Experience.ToString();
            if (_goldTextBox != null)
                _goldTextBox.Text = bicFile.Gold.ToString();
            if (_ageTextBox != null)
                _ageTextBox.Text = bicFile.Age.ToString();
            if (_biographyTextBox != null)
                _biographyTextBox.Text = creature.Description?.GetString(0) ?? "";
        }
        else
        {
            // Clear BIC fields for UTC files
            if (_experienceTextBox != null)
                _experienceTextBox.Text = "";
            if (_goldTextBox != null)
                _goldTextBox.Text = "";
            if (_ageTextBox != null)
                _ageTextBox.Text = "";
            if (_biographyTextBox != null)
                _biographyTextBox.Text = "";
        }

        // Defer clearing _isLoading until after dispatcher processes queued TextChanged events
        // TextBox.Text changes queue TextChanged events to the dispatcher, which fire after this method returns
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _isLoading = false, Avalonia.Threading.DispatcherPriority.Background);
    }

    public void ClearPanel()
    {
        _isLoading = true; // Prevent change events during clear

        if (_firstNameTextBox != null)
            _firstNameTextBox.Text = "";
        if (_lastNameTextBox != null)
            _lastNameTextBox.Text = "";
        if (_raceComboBox != null)
            _raceComboBox.SelectedIndex = -1;
        if (_subraceTextBox != null)
            _subraceTextBox.Text = "";
        if (_deityTextBox != null)
            _deityTextBox.Text = "";
        if (_portraitComboBox != null)
            _portraitComboBox.SelectedIndex = -1;
        if (_portraitIdTextBox != null)
            _portraitIdTextBox.Text = "";
        if (_portraitResRefTextBox != null)
            _portraitResRefTextBox.Text = "";
        if (_soundSetComboBox != null)
            _soundSetComboBox.SelectedIndex = -1;
        if (_conversationTextBox != null)
            _conversationTextBox.Text = "";

        // Clear BIC-specific fields
        if (_experienceTextBox != null)
            _experienceTextBox.Text = "";
        if (_goldTextBox != null)
            _goldTextBox.Text = "";
        if (_ageTextBox != null)
            _ageTextBox.Text = "";
        if (_biographyTextBox != null)
            _biographyTextBox.Text = "";

        _isLoading = false;
    }

    #endregion

    #region Selection Helpers

    private void SelectRace(byte raceId)
    {
        if (_raceComboBox == null) return;

        for (int i = 0; i < _raceComboBox.Items.Count; i++)
        {
            if (_raceComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is byte id && id == raceId)
            {
                _raceComboBox.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it (custom race from module)
        var raceName = _displayService?.GetRaceName(raceId) ?? $"Race {raceId}";
        _raceComboBox.Items.Add(new ComboBoxItem
        {
            Content = raceName,
            Tag = raceId
        });
        _raceComboBox.SelectedIndex = _raceComboBox.Items.Count - 1;
    }

    private void SelectPortrait(ushort portraitId, string? portraitResRef = null)
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

        // If not found, add it with appropriate display name
        // If we have a portrait ResRef but no 2DA match, use the ResRef as display name
        string name;
        if (portraitId == 0 && !string.IsNullOrEmpty(portraitResRef))
        {
            // Portrait string field is set but not found in portraits.2da (custom portrait)
            name = portraitResRef;
        }
        else
        {
            name = _displayService?.GetPortraitName(portraitId) ?? $"Portrait {portraitId}";
        }

        _portraitComboBox.Items.Add(new ComboBoxItem
        {
            Content = name,
            Tag = portraitId
        });
        _portraitComboBox.SelectedIndex = _portraitComboBox.Items.Count - 1;
    }

    private void SelectSoundSet(ushort soundSetId)
    {
        if (_soundSetComboBox == null) return;

        for (int i = 0; i < _soundSetComboBox.Items.Count; i++)
        {
            if (_soundSetComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is ushort id && id == soundSetId)
            {
                _soundSetComboBox.SelectedIndex = i;
                UpdateSoundsetTypeAvailability(soundSetId);
                return;
            }
        }

        // If not found, add it with proper name lookup
        var soundSetName = _displayService?.GetSoundSetName(soundSetId) ?? $"Sound Set {soundSetId}";
        _soundSetComboBox.Items.Add(new ComboBoxItem
        {
            Content = soundSetName,
            Tag = soundSetId
        });
        _soundSetComboBox.SelectedIndex = _soundSetComboBox.Items.Count - 1;
        UpdateSoundsetTypeAvailability(soundSetId);
    }

    #endregion

    #region Event Handlers

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        if (sender == _firstNameTextBox)
        {
            _currentCreature.FirstName?.SetString(0, _firstNameTextBox?.Text ?? "");
        }
        else if (sender == _lastNameTextBox)
        {
            _currentCreature.LastName?.SetString(0, _lastNameTextBox?.Text ?? "");
        }
        else if (sender == _subraceTextBox)
        {
            _currentCreature.Subrace = _subraceTextBox?.Text ?? "";
        }
        else if (sender == _deityTextBox)
        {
            _currentCreature.Deity = _deityTextBox?.Text ?? "";
        }
        else if (sender == _conversationTextBox)
        {
            _currentCreature.Conversation = _conversationTextBox?.Text ?? "";
        }
        else if (sender == _biographyTextBox)
        {
            _currentCreature.Description?.SetString(0, _biographyTextBox?.Text ?? "");
        }

        CharacterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBicFieldChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentCreature is not BicFile bicFile) return;

        if (sender == _experienceTextBox)
        {
            if (uint.TryParse(_experienceTextBox?.Text, out var exp))
                bicFile.Experience = exp;
        }
        else if (sender == _goldTextBox)
        {
            if (uint.TryParse(_goldTextBox?.Text, out var gold))
                bicFile.Gold = gold;
        }
        else if (sender == _ageTextBox)
        {
            if (int.TryParse(_ageTextBox?.Text, out var age))
                bicFile.Age = age;
        }

        CharacterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        if (sender == _raceComboBox &&
            _raceComboBox?.SelectedItem is ComboBoxItem raceItem &&
            raceItem.Tag is byte raceId)
        {
            _currentCreature.Race = raceId;
            CharacterChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (sender == _soundSetComboBox &&
            _soundSetComboBox?.SelectedItem is ComboBoxItem soundItem &&
            soundItem.Tag is ushort soundSetId)
        {
            _currentCreature.SoundSetFile = soundSetId;
            UpdateSoundsetTypeAvailability(soundSetId);
            CharacterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPortraitSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        if (_portraitComboBox?.SelectedItem is ComboBoxItem item &&
            item.Tag is ushort portraitId)
        {
            _currentCreature.PortraitId = portraitId;

            // Also update the PortraitId text field to stay in sync
            if (_portraitIdTextBox != null)
                _portraitIdTextBox.Text = portraitId.ToString();

            CharacterChanged?.Invoke(this, EventArgs.Empty);
            PortraitChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPortraitIdTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        if (ushort.TryParse(_portraitIdTextBox?.Text, out var portraitId))
        {
            _currentCreature.PortraitId = portraitId;

            // Update the dropdown to match (if portrait exists in list)
            SelectPortrait(portraitId);

            CharacterChanged?.Invoke(this, EventArgs.Empty);
            PortraitChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPortraitResRefTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.Portrait = _portraitResRefTextBox?.Text ?? "";
        CharacterChanged?.Invoke(this, EventArgs.Empty);
        PortraitChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
