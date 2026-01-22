using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Ssf;
using Radoub.UI.Services;

namespace Quartermaster.Views;

/// <summary>
/// Soundset browser window with filtering and preview playback.
/// </summary>
public partial class SoundsetBrowserWindow : Window
{
    private readonly IGameDataService? _gameDataService;
    private readonly AudioService? _audioService;
    private readonly ListBox _soundsetListBox;
    private readonly TextBox _searchBox;
    private readonly TextBlock _soundsetCountLabel;
    private readonly TextBlock _selectedSoundsetLabel;
    private readonly TextBlock _soundsetNameLabel;
    private readonly ComboBox _soundTypeComboBox;
    private readonly ComboBox _genderFilterComboBox;
    private readonly Button _playButton;
    private readonly Button _stopButton;

    private List<SoundsetInfo> _allSoundsets = new();
    private List<SoundsetInfo> _filteredSoundsets = new();
    private SoundsetInfo? _selectedSoundset;
    private List<SoundTypeItem> _soundTypeItems = new();

    /// <summary>
    /// Gets the selected soundset ID, or null if cancelled.
    /// </summary>
    public ushort? SelectedSoundsetId => _selectedSoundset?.Id;

    /// <summary>
    /// Parameterless constructor for XAML designer.
    /// </summary>
    public SoundsetBrowserWindow()
    {
        InitializeComponent();
        _soundsetListBox = this.FindControl<ListBox>("SoundsetListBox")!;
        _searchBox = this.FindControl<TextBox>("SearchBox")!;
        _soundsetCountLabel = this.FindControl<TextBlock>("SoundsetCountLabel")!;
        _selectedSoundsetLabel = this.FindControl<TextBlock>("SelectedSoundsetLabel")!;
        _soundsetNameLabel = this.FindControl<TextBlock>("SoundsetNameLabel")!;
        _soundTypeComboBox = this.FindControl<ComboBox>("SoundTypeComboBox")!;
        _genderFilterComboBox = this.FindControl<ComboBox>("GenderFilterComboBox")!;
        _playButton = this.FindControl<Button>("PlayButton")!;
        _stopButton = this.FindControl<Button>("StopButton")!;
    }

    /// <summary>
    /// Creates a new soundset browser window.
    /// </summary>
    /// <param name="gameDataService">Game data service for 2DA lookups</param>
    /// <param name="audioService">Audio service for preview playback</param>
    public SoundsetBrowserWindow(IGameDataService gameDataService, AudioService audioService) : this()
    {
        _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

        _audioService.PlaybackStopped += OnPlaybackStopped;

        InitializeGenderFilter();
        InitializeSoundTypes();
        LoadSoundsets();
    }

    private void InitializeGenderFilter()
    {
        _genderFilterComboBox.Items.Clear();
        _genderFilterComboBox.Items.Add(new ComboBoxItem { Content = "All", Tag = -1 });
        _genderFilterComboBox.Items.Add(new ComboBoxItem { Content = "Male", Tag = 0 });
        _genderFilterComboBox.Items.Add(new ComboBoxItem { Content = "Female", Tag = 1 });
        _genderFilterComboBox.Items.Add(new ComboBoxItem { Content = "Other", Tag = 2 });
        _genderFilterComboBox.SelectedIndex = 0;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeSoundTypes()
    {
        _soundTypeItems = new List<SoundTypeItem>
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

        _soundTypeComboBox.ItemsSource = _soundTypeItems;
        _soundTypeComboBox.SelectedIndex = 0;
    }

    private void LoadSoundsets()
    {
        if (_gameDataService == null) return;

        _allSoundsets.Clear();

        for (int i = 0; i < 500; i++)
        {
            var label = _gameDataService.Get2DAValue("soundset", i, "LABEL");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (_allSoundsets.Count > 50)
                    break;
                continue;
            }

            var displayName = GetSoundsetDisplayName((ushort)i);

            // Get gender from GENDER column (0=Male, 1=Female, 2+=Other)
            int gender = -1;
            var genderStr = _gameDataService.Get2DAValue("soundset", i, "GENDER");
            if (!string.IsNullOrEmpty(genderStr) && genderStr != "****")
                int.TryParse(genderStr, out gender);

            _allSoundsets.Add(new SoundsetInfo
            {
                Id = (ushort)i,
                Label = label,
                DisplayName = displayName,
                Gender = gender
            });
        }

        UpdateSoundsetList();
    }

    private string GetSoundsetDisplayName(ushort soundsetId)
    {
        if (_gameDataService == null)
            return $"Soundset {soundsetId}";

        // Try STRREF for localized name first
        var strRef = _gameDataService.Get2DAValue("soundset", soundsetId, "STRREF");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fall back to LABEL column
        var label = _gameDataService.Get2DAValue("soundset", soundsetId, "LABEL");
        if (!string.IsNullOrEmpty(label) && label != "****")
            return label;

        return $"Soundset {soundsetId}";
    }

    private void UpdateSoundsetList()
    {
        _soundsetListBox.Items.Clear();

        var searchText = _searchBox?.Text?.ToLowerInvariant() ?? "";

        // Get gender filter value
        int genderFilter = -1;
        if (_genderFilterComboBox.SelectedItem is ComboBoxItem genderItem && genderItem.Tag is int gender)
            genderFilter = gender;

        _filteredSoundsets = _allSoundsets
            .Where(s =>
            {
                // Gender filter: -1 = All, 0 = Male, 1 = Female, 2 = Other (anything >= 2)
                if (genderFilter >= 0)
                {
                    if (genderFilter == 2)
                    {
                        // "Other" matches anything that's not 0 (male) or 1 (female)
                        if (s.Gender == 0 || s.Gender == 1)
                            return false;
                    }
                    else if (s.Gender != genderFilter)
                    {
                        return false;
                    }
                }

                // Search filter
                if (!string.IsNullOrEmpty(searchText))
                {
                    return s.DisplayName.ToLowerInvariant().Contains(searchText) ||
                           s.Label.ToLowerInvariant().Contains(searchText);
                }
                return true;
            })
            .OrderBy(s => s.DisplayName)
            .ToList();

        foreach (var soundset in _filteredSoundsets)
        {
            _soundsetListBox.Items.Add(new ListBoxItem
            {
                Content = soundset.DisplayName,
                Tag = soundset
            });
        }

        _soundsetCountLabel.Text = $"{_filteredSoundsets.Count} soundset{(_filteredSoundsets.Count == 1 ? "" : "s")}";
    }

    private void OnGenderFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateSoundsetList();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateSoundsetList();
    }

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        _searchBox.Text = "";
    }

    private void OnSoundsetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_soundsetListBox.SelectedItem is ListBoxItem item && item.Tag is SoundsetInfo soundset)
        {
            _selectedSoundset = soundset;
            _selectedSoundsetLabel.Text = soundset.DisplayName;
            _soundsetNameLabel.Text = $"{soundset.DisplayName}\n(ID: {soundset.Id})";

            // Update sound type availability
            UpdateSoundTypeAvailability(soundset.Id);

            // Enable play button only if currently selected sound type is available
            UpdatePlayButtonState();
        }
        else
        {
            _selectedSoundset = null;
            _selectedSoundsetLabel.Text = "(none)";
            _soundsetNameLabel.Text = "";
            _playButton.IsEnabled = false;

            // Reset all to available when no soundset selected
            foreach (var typeItem in _soundTypeItems)
            {
                typeItem.IsAvailable = true;
            }
        }
    }

    private void UpdateSoundTypeAvailability(ushort soundsetId)
    {
        if (_gameDataService == null) return;

        var ssf = _gameDataService.GetSoundset(soundsetId);
        if (ssf == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not load soundset {soundsetId} for availability check");
            return;
        }

        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Checking sound availability for soundset {soundsetId}:");

        foreach (var typeItem in _soundTypeItems)
        {
            var entry = ssf.GetEntry(typeItem.SoundType);
            bool hasSound = entry != null && entry.HasSound;
            typeItem.IsAvailable = hasSound;

            var resRef = entry?.ResRef ?? "(null)";
            var strRef = entry?.StringRef ?? 0;
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"  {typeItem.Name}: HasSound={hasSound}, ResRef={resRef}, StrRef={strRef}");
        }

        // Force ComboBox to refresh display
        _soundTypeComboBox.ItemsSource = null;
        _soundTypeComboBox.ItemsSource = _soundTypeItems;

        // Re-select first available or keep current selection
        var currentIndex = _soundTypeComboBox.SelectedIndex;
        if (currentIndex >= 0 && currentIndex < _soundTypeItems.Count && _soundTypeItems[currentIndex].IsAvailable)
        {
            _soundTypeComboBox.SelectedIndex = currentIndex;
        }
        else
        {
            // Find first available
            var firstAvailable = _soundTypeItems.FindIndex(t => t.IsAvailable);
            _soundTypeComboBox.SelectedIndex = firstAvailable >= 0 ? firstAvailable : 0;
        }
    }

    private void UpdatePlayButtonState()
    {
        if (_selectedSoundset == null)
        {
            _playButton.IsEnabled = false;
            return;
        }

        if (_soundTypeComboBox.SelectedItem is SoundTypeItem selectedType)
        {
            _playButton.IsEnabled = selectedType.IsAvailable;
        }
        else
        {
            _playButton.IsEnabled = false;
        }
    }

    private void OnSoundTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdatePlayButtonState();
    }

    private async void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedSoundset == null || _gameDataService == null || _audioService == null)
            return;

        if (_soundTypeComboBox.SelectedItem is not SoundTypeItem selectedType)
            return;

        var soundsetId = _selectedSoundset.Id;

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

        _playButton.IsEnabled = false;
        _stopButton.IsEnabled = true;

        try
        {
            // Load sound from GameDataService (BIF archives)
            var soundData = _gameDataService.FindResource(entry.ResRef, ResourceTypes.Wav);
            if (soundData != null)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"ssf_{entry.ResRef}.wav");
                await File.WriteAllBytesAsync(tempPath, soundData);
                _audioService.Play(tempPath);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Playing: {entry.ResRef}");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound not found: {entry.ResRef}");
                _playButton.IsEnabled = true;
                _stopButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to play sound: {ex.Message}");
            _playButton.IsEnabled = true;
            _stopButton.IsEnabled = false;
        }
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        _audioService?.Stop();
        _stopButton.IsEnabled = false;
        _playButton.IsEnabled = _selectedSoundset != null;
    }

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _playButton.IsEnabled = _selectedSoundset != null;
            _stopButton.IsEnabled = false;
        });
    }

    private void OnSoundsetDoubleClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedSoundset != null)
        {
            _audioService?.Stop();
            Close(_selectedSoundset.Id);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        _audioService?.Stop();
        if (_selectedSoundset != null)
        {
            Close(_selectedSoundset.Id);
        }
        else
        {
            Close(null);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _audioService?.Stop();
        Close(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_audioService != null)
        {
            _audioService.Stop();
            _audioService.PlaybackStopped -= OnPlaybackStopped;
        }
        base.OnClosed(e);
    }

    #region Title Bar Events

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion

    private class SoundsetInfo
    {
        public ushort Id { get; set; }
        public string Label { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Gender { get; set; } = -1; // 0=Male, 1=Female, 2+=Other
    }

    private class SoundTypeItem : INotifyPropertyChanged
    {
        private bool _isAvailable = true;

        public string Name { get; set; } = "";
        public SsfSoundType SoundType { get; set; }

        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                if (_isAvailable != value)
                {
                    _isAvailable = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(Opacity));
                }
            }
        }

        public string DisplayName => IsAvailable ? Name : $"{Name} (N/A)";

        // Use opacity for theme-aware dimming of unavailable items
        public double Opacity => IsAvailable ? 1.0 : 0.5;

        public override string ToString() => DisplayName;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
