using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Bic;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

public partial class CharacterPanel : UserControl
{
    private TextBox? _firstNameTextBox;
    private TextBox? _lastNameTextBox;
    private TextBox? _subraceTextBox;
    private TextBox? _deityTextBox;
    private ComboBox? _soundSetComboBox;
    private TextBox? _conversationTextBox;
    private Button? _browseConversationButton;
    private Button? _clearConversationButton;

    // Conversation row visibility controls
    private TextBlock? _conversationLabel;
    private Grid? _conversationRow;

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
    private Func<string, string?>? _conversationResolver;

    public event EventHandler? CharacterChanged;

    public CharacterPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _firstNameTextBox = this.FindControl<TextBox>("FirstNameTextBox");
        _lastNameTextBox = this.FindControl<TextBox>("LastNameTextBox");
        _subraceTextBox = this.FindControl<TextBox>("SubraceTextBox");
        _deityTextBox = this.FindControl<TextBox>("DeityTextBox");
        _soundSetComboBox = this.FindControl<ComboBox>("SoundSetComboBox");
        _conversationTextBox = this.FindControl<TextBox>("ConversationTextBox");
        _browseConversationButton = this.FindControl<Button>("BrowseConversationButton");
        _clearConversationButton = this.FindControl<Button>("ClearConversationButton");

        // Conversation row visibility controls
        _conversationLabel = this.FindControl<TextBlock>("ConversationLabel");
        _conversationRow = this.FindControl<Grid>("ConversationRow");

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
        if (_soundSetComboBox != null)
            _soundSetComboBox.SelectionChanged += OnSelectionChanged;
        if (_conversationTextBox != null)
            _conversationTextBox.TextChanged += OnTextChanged;
        if (_clearConversationButton != null)
            _clearConversationButton.Click += OnClearConversationClick;

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
        LoadSoundSetData();
    }

    public void SetConversationResolver(Func<string, string?> resolver)
    {
        _conversationResolver = resolver;
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
        if (_subraceTextBox != null)
            _subraceTextBox.Text = creature.Subrace ?? "";
        if (_deityTextBox != null)
            _deityTextBox.Text = creature.Deity ?? "";

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

        // If not found, add it
        _soundSetComboBox.Items.Add(new ComboBoxItem
        {
            Content = $"Sound Set {soundSetId}",
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

        if (sender == _soundSetComboBox &&
            _soundSetComboBox?.SelectedItem is ComboBoxItem item &&
            item.Tag is ushort soundSetId)
        {
            _currentCreature.SoundSetFile = soundSetId;
            CharacterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnClearConversationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_conversationTextBox != null)
            _conversationTextBox.Text = "";
    }

    public void ClearPanel()
    {
        if (_firstNameTextBox != null)
            _firstNameTextBox.Text = "";
        if (_lastNameTextBox != null)
            _lastNameTextBox.Text = "";
        if (_subraceTextBox != null)
            _subraceTextBox.Text = "";
        if (_deityTextBox != null)
            _deityTextBox.Text = "";
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
}
