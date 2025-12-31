using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
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

    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;
    private bool _isLoading;
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

        // Wire up events
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

        _isLoading = false;
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
    }
}
