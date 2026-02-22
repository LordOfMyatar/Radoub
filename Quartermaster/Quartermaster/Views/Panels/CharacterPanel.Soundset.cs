using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Ssf;
using Radoub.UI.Services;

namespace Quartermaster.Views.Panels;

/// <summary>
/// Soundset preview: type dropdown, playback, availability tracking (#916).
/// </summary>
public partial class CharacterPanel
{
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
    /// Item for the soundset type dropdown with availability tracking.
    /// </summary>
    private class SoundsetTypeItem : INotifyPropertyChanged
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

    private void InitializeSoundsetTypeComboBox()
    {
        if (_soundsetTypeComboBox == null) return;

        _soundsetTypeItems = new List<SoundsetTypeItem>
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

        _soundsetTypeComboBox.ItemsSource = _soundsetTypeItems;
        _soundsetTypeComboBox.SelectedIndex = 0; // Hello
        _soundsetTypeComboBox.SelectionChanged += OnSoundsetTypeSelectionChanged;
    }

    private void OnSoundsetTypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateSoundsetPlayButtonState();
    }

    private void UpdateSoundsetPlayButtonState()
    {
        if (_soundsetPlayButton == null) return;

        if (_currentCreature == null || _currentCreature.SoundSetFile == ushort.MaxValue)
        {
            _soundsetPlayButton.IsEnabled = false;
            return;
        }

        if (_soundsetTypeComboBox?.SelectedItem is SoundsetTypeItem selectedType)
        {
            _soundsetPlayButton.IsEnabled = selectedType.IsAvailable;
        }
        else
        {
            _soundsetPlayButton.IsEnabled = false;
        }
    }

    private void UpdateSoundsetTypeAvailability(ushort soundsetId)
    {
        if (_gameDataService == null || _soundsetTypeComboBox == null) return;

        if (soundsetId == ushort.MaxValue)
        {
            // No soundset - mark all as available (greyed button handles this)
            foreach (var item in _soundsetTypeItems)
                item.IsAvailable = true;
            return;
        }

        var ssf = _gameDataService.GetSoundset(soundsetId);
        if (ssf == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not load soundset {soundsetId} for availability check");
            return;
        }

        foreach (var typeItem in _soundsetTypeItems)
        {
            var entry = ssf.GetEntry(typeItem.SoundType);
            typeItem.IsAvailable = entry != null && entry.HasSound;
        }

        // Force ComboBox to refresh display
        _soundsetTypeComboBox.ItemsSource = null;
        _soundsetTypeComboBox.ItemsSource = _soundsetTypeItems;

        // Re-select first available or keep current selection
        var currentIndex = _soundsetTypeComboBox.SelectedIndex;
        if (currentIndex >= 0 && currentIndex < _soundsetTypeItems.Count && _soundsetTypeItems[currentIndex].IsAvailable)
        {
            _soundsetTypeComboBox.SelectedIndex = currentIndex;
        }
        else
        {
            // Find first available
            var firstAvailable = _soundsetTypeItems.FindIndex(t => t.IsAvailable);
            _soundsetTypeComboBox.SelectedIndex = firstAvailable >= 0 ? firstAvailable : 0;
        }

        UpdateSoundsetPlayButtonState();
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
