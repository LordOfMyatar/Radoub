using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Ssf;
using Radoub.Formats.Common;
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
    private readonly Button _playButton;
    private readonly Button _stopButton;

    private List<SoundsetInfo> _allSoundsets = new();
    private List<SoundsetInfo> _filteredSoundsets = new();
    private SoundsetInfo? _selectedSoundset;

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

        InitializeSoundTypes();
        LoadSoundsets();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeSoundTypes()
    {
        var items = new List<SoundTypeItem>
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

        _soundTypeComboBox.ItemsSource = items;
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

            _allSoundsets.Add(new SoundsetInfo
            {
                Id = (ushort)i,
                Label = label,
                DisplayName = displayName
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

        _filteredSoundsets = _allSoundsets
            .Where(s =>
            {
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
            _playButton.IsEnabled = true;
        }
        else
        {
            _selectedSoundset = null;
            _selectedSoundsetLabel.Text = "(none)";
            _soundsetNameLabel.Text = "";
            _playButton.IsEnabled = false;
        }
    }

    private void OnSoundTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Sound type changed - no immediate action needed
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

    private class SoundsetInfo
    {
        public ushort Id { get; set; }
        public string Label { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    private class SoundTypeItem
    {
        public string Name { get; set; } = "";
        public SsfSoundType SoundType { get; set; }
        public override string ToString() => Name;
    }
}
