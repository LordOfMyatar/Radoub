using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Bic;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Ssf;
using Radoub.Formats.Utc;
using Radoub.UI.Services;
using Radoub.UI.Views;

namespace Quartermaster.Views.Panels;

public partial class CharacterPanel : UserControl
{
    private TextBox? _firstNameTextBox;
    private TextBox? _lastNameTextBox;
    private ComboBox? _raceComboBox;
    private TextBox? _subraceTextBox;
    private TextBox? _deityTextBox;
    private ComboBox? _portraitComboBox;
    private ComboBox? _soundSetComboBox;
    private TextBox? _conversationTextBox;
    private Button? _browseConversationButton;
    private Button? _clearConversationButton;
    private Button? _browsePortraitButton;

    // Conversation row visibility controls
    private TextBlock? _conversationLabel;
    private Grid? _conversationRow;

    // Soundset preview controls (#916)
    private ComboBox? _soundsetTypeComboBox;
    private Button? _soundsetPlayButton;
    private AudioService? _audioService;

    // BIC-specific controls
    private Border? _playerCharacterSection;
    private Border? _biographySection;
    private TextBox? _experienceTextBox;
    private TextBox? _goldTextBox;
    private TextBox? _ageTextBox;
    private TextBox? _biographyTextBox;

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

        _firstNameTextBox = this.FindControl<TextBox>("FirstNameTextBox");
        _lastNameTextBox = this.FindControl<TextBox>("LastNameTextBox");
        _raceComboBox = this.FindControl<ComboBox>("RaceComboBox");
        _subraceTextBox = this.FindControl<TextBox>("SubraceTextBox");
        _deityTextBox = this.FindControl<TextBox>("DeityTextBox");
        _portraitComboBox = this.FindControl<ComboBox>("PortraitComboBox");
        _soundSetComboBox = this.FindControl<ComboBox>("SoundSetComboBox");
        _conversationTextBox = this.FindControl<TextBox>("ConversationTextBox");
        _browseConversationButton = this.FindControl<Button>("BrowseConversationButton");
        _clearConversationButton = this.FindControl<Button>("ClearConversationButton");
        _browsePortraitButton = this.FindControl<Button>("BrowsePortraitButton");

        // Conversation row visibility controls
        _conversationLabel = this.FindControl<TextBlock>("ConversationLabel");
        _conversationRow = this.FindControl<Grid>("ConversationRow");

        // Soundset preview controls (#916)
        _soundsetTypeComboBox = this.FindControl<ComboBox>("SoundsetTypeComboBox");
        _soundsetPlayButton = this.FindControl<Button>("SoundsetPlayButton");

        // BIC-specific controls
        _playerCharacterSection = this.FindControl<Border>("PlayerCharacterSection");
        _biographySection = this.FindControl<Border>("BiographySection");
        _experienceTextBox = this.FindControl<TextBox>("ExperienceTextBox");
        _goldTextBox = this.FindControl<TextBox>("GoldTextBox");
        _ageTextBox = this.FindControl<TextBox>("AgeTextBox");
        _biographyTextBox = this.FindControl<TextBox>("BiographyTextBox");

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
    }

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

        // Identity
        SelectRace(creature.Race);
        if (_subraceTextBox != null)
            _subraceTextBox.Text = creature.Subrace ?? "";
        if (_deityTextBox != null)
            _deityTextBox.Text = creature.Deity ?? "";
        SelectPortrait(creature.PortraitId);

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

    private void SelectSoundSet(ushort soundSetId)
    {
        if (_soundSetComboBox == null) return;

        for (int i = 0; i < _soundSetComboBox.Items.Count; i++)
        {
            if (_soundSetComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is ushort id && id == soundSetId)
            {
                _soundSetComboBox.SelectedIndex = i;
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
    }

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
            CharacterChanged?.Invoke(this, EventArgs.Empty);
            PortraitChanged?.Invoke(this, EventArgs.Empty);
        }
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

    private async void OnBrowsePortraitClick(object? sender, RoutedEventArgs e)
    {
        if (_gameDataService == null || _itemIconService == null)
            return;

        var browser = new PortraitBrowserWindow(_gameDataService, _itemIconService);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var result = await browser.ShowDialog<ushort?>(parentWindow);
            if (result.HasValue)
            {
                SelectPortrait(result.Value);
                // Trigger change event
                if (_currentCreature != null)
                {
                    _currentCreature.PortraitId = result.Value;
                    CharacterChanged?.Invoke(this, EventArgs.Empty);
                    PortraitChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    public void ClearPanel()
    {
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
    }

    #region Soundset Preview (#916)

    /// <summary>
    /// Sets the audio service for soundset preview playback.
    /// </summary>
    public void SetAudioService(AudioService? service)
    {
        _audioService = service;
        if (_audioService != null)
        {
            _audioService.PlaybackStopped += OnPlaybackStopped;
        }
    }

    /// <summary>
    /// Item for the soundset type dropdown.
    /// </summary>
    private class SoundsetTypeItem
    {
        public string Name { get; set; } = "";
        public SsfSoundType SoundType { get; set; }
        public override string ToString() => Name;
    }

    private void InitializeSoundsetTypeComboBox()
    {
        if (_soundsetTypeComboBox == null) return;

        var items = new List<SoundsetTypeItem>
        {
            new() { Name = "Hello", SoundType = SsfSoundType.Hello },
            new() { Name = "Goodbye", SoundType = SsfSoundType.Goodbye },
            new() { Name = "Yes", SoundType = SsfSoundType.Yes },
            new() { Name = "No", SoundType = SsfSoundType.No },
            new() { Name = "Attack", SoundType = SsfSoundType.Attack },
            new() { Name = "Battlecry", SoundType = SsfSoundType.Battlecry1 },
            new() { Name = "Taunt", SoundType = SsfSoundType.Taunt },
            new() { Name = "Death", SoundType = SsfSoundType.Death },
            new() { Name = "Laugh", SoundType = SsfSoundType.Laugh },
            new() { Name = "Selected", SoundType = SsfSoundType.Selected },
        };

        _soundsetTypeComboBox.ItemsSource = items;
        _soundsetTypeComboBox.SelectedIndex = 0; // Hello
    }

    private async void OnSoundsetPlayClick(object? sender, RoutedEventArgs e)
    {
        if (_soundsetTypeComboBox?.SelectedItem is not SoundsetTypeItem selectedType)
            return;

        if (_currentCreature == null || _gameDataService == null || _audioService == null)
            return;

        var soundsetId = _currentCreature.SoundSetFile;
        if (soundsetId == ushort.MaxValue)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "No soundset assigned to creature");
            return;
        }

        // Get the soundset
        var ssf = _gameDataService.GetSoundset(soundsetId);
        if (ssf == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Cannot load soundset ID {soundsetId}");
            return;
        }

        // Get the sound entry
        var entry = ssf.GetEntry(selectedType.SoundType);
        if (entry == null || !entry.HasSound)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"No sound for '{selectedType.Name}' in soundset {soundsetId}");
            return;
        }

        // Disable play button during playback
        if (_soundsetPlayButton != null) _soundsetPlayButton.IsEnabled = false;

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Playing soundset sound: {entry.ResRef}");

        try
        {
            // Load sound from GameDataService (BIF archives)
            var soundData = _gameDataService.FindResource(entry.ResRef, ResourceTypes.Wav);
            if (soundData != null)
            {
                // Log first bytes for format diagnosis
                var headerBytes = soundData.Length >= 16 ? soundData[..16] : soundData;
                var hex = BitConverter.ToString(headerBytes).Replace("-", " ");
                var ascii = new string(headerBytes.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Found sound in BIF: {entry.ResRef} ({soundData.Length} bytes) - Header: {hex} | {ascii}");
                // Extract to temp file and play
                var tempPath = Path.Combine(Path.GetTempPath(), $"ssf_{entry.ResRef}.wav");
                await File.WriteAllBytesAsync(tempPath, soundData);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Wrote temp file: {tempPath}");
                _audioService.Play(tempPath);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Playing: {entry.ResRef} (from BIF)");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound not found in GameDataService: {entry.ResRef}");
            if (_soundsetPlayButton != null) _soundsetPlayButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to play sound '{entry.ResRef}': {ex.GetType().Name}: {ex.Message}");
            if (_soundsetPlayButton != null) _soundsetPlayButton.IsEnabled = true;
        }
    }

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_soundsetPlayButton != null) _soundsetPlayButton.IsEnabled = true;
        });
    }

    #endregion
}
