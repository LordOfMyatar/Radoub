using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DialogEditor.Services;

namespace DialogEditor.Views
{
    public partial class SoundBrowserWindow : Window
    {
        private readonly SoundService _soundService;
        private readonly AudioService _audioService;
        private Dictionary<string, List<string>> _allSounds;
        private List<string> _filteredSounds;
        private string? _selectedSound;
        private string _currentCategory = "Ambient";

        public string? SelectedSound => _selectedSound;

        public SoundBrowserWindow()
        {
            InitializeComponent();
            _soundService = new SoundService(SettingsService.Instance);
            _audioService = new AudioService();
            _allSounds = new Dictionary<string, List<string>>();
            _filteredSounds = new List<string>();

            LoadSounds();

            // Clean up audio on window close
            Closing += (s, e) => _audioService.Dispose();
        }

        private void LoadSounds()
        {
            try
            {
                var basePath = SettingsService.Instance.BaseGameInstallPath;
                if (string.IsNullOrEmpty(basePath))
                {
                    FileCountLabel.Text = "⚠ Base game path not configured - go to Settings to set game directory";
                    FileCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Sound Browser: Base game path not configured");
                    return;
                }

                if (!Directory.Exists(basePath))
                {
                    FileCountLabel.Text = $"⚠ Game path not found: {basePath}";
                    FileCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound Browser: Game path does not exist: {UnifiedLogger.SanitizePath(basePath)}");
                    return;
                }

                _allSounds = _soundService.GetSoundsByCategory();

                var totalSounds = _allSounds.Values.Sum(list => list.Count);
                if (totalSounds == 0)
                {
                    FileCountLabel.Text = "⚠ No loose sound files found. NWN sounds are typically in .bif archives (not yet supported). " +
                                         "You can extract sounds to game folders or type filenames directly.";
                    FileCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"No loose sound files found in: {basePath}. Sounds are likely in .bif archives.");
                }

                // Select first category by default
                if (CategoryListBox.SelectedIndex == -1)
                {
                    CategoryListBox.SelectedIndex = 0;
                }

                UpdateSoundList();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load sounds: {ex.Message}");
                FileCountLabel.Text = $"❌ Error loading sounds: {ex.Message}";
                FileCountLabel.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void OnCategorySelected(object? sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is ListBoxItem item)
            {
                _currentCategory = item.Content?.ToString() ?? "Ambient";
                UpdateSoundList();
            }
        }

        private void UpdateSoundList()
        {
            SoundListBox.Items.Clear();

            List<string> soundsToDisplay;

            if (_currentCategory == "Recent")
            {
                soundsToDisplay = _soundService.GetRecentSounds();
            }
            else
            {
                soundsToDisplay = _allSounds.ContainsKey(_currentCategory)
                    ? _allSounds[_currentCategory]
                    : new List<string>();
            }

            // Apply search filter if active
            var searchText = SearchBox?.Text?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                soundsToDisplay = soundsToDisplay
                    .Where(s => s.ToLowerInvariant().Contains(searchText))
                    .ToList();
            }

            _filteredSounds = soundsToDisplay;

            foreach (var sound in soundsToDisplay)
            {
                SoundListBox.Items.Add(sound);
            }

            FileCountLabel.Text = $"{soundsToDisplay.Count} sound{(soundsToDisplay.Count == 1 ? "" : "s")}";
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSoundList();
        }

        private void OnSoundSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (SoundListBox.SelectedItem is string soundName)
            {
                _selectedSound = soundName;
                SelectedSoundLabel.Text = soundName;
                PlayButton.IsEnabled = true;

                // Validate sound file format against NWN specs
                var soundPath = FindSoundFile(soundName);
                if (soundPath != null)
                {
                    try
                    {
                        var validation = SoundValidator.Validate(soundPath, isVoiceOrSfx: true);

                        if (validation.HasIssues)
                        {
                            var issues = string.Join(", ", validation.Errors.Concat(validation.Warnings));
                            FileCountLabel.Text = validation.IsValid
                                ? $"⚠ {issues}"
                                : $"❌ {issues}";
                            FileCountLabel.Foreground = validation.IsValid
                                ? new SolidColorBrush(Colors.Orange)
                                : new SolidColorBrush(Colors.Red);
                        }
                        else if (!string.IsNullOrEmpty(validation.FormatInfo))
                        {
                            FileCountLabel.Text = $"✓ {validation.FormatInfo}";
                            FileCountLabel.Foreground = new SolidColorBrush(Colors.Green);
                        }
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR, $"Validation error: {ex.Message}");
                    }
                }
            }
            else
            {
                _selectedSound = null;
                SelectedSoundLabel.Text = "(none)";
                PlayButton.IsEnabled = false;
            }
        }

        private void OnSoundDoubleClicked(object? sender, RoutedEventArgs e)
        {
            // Double-click selects and closes
            if (_selectedSound != null)
            {
                _soundService.AddRecentSound(_selectedSound);

                // NWN expects sound names WITHOUT file extension
                var soundNameWithoutExtension = Path.GetFileNameWithoutExtension(_selectedSound);
                Close(soundNameWithoutExtension);
            }
        }

        private void OnPlayClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedSound == null)
                return;

            try
            {
                // Find the sound file in either user or game paths
                var soundPath = FindSoundFile(_selectedSound);
                if (soundPath == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound file not found: {_selectedSound}");
                    CurrentSoundLabel.Text = $"⚠ File not found: {_selectedSound}";
                    CurrentSoundLabel.Foreground = new SolidColorBrush(Colors.Orange);
                    return;
                }

                _audioService.Play(soundPath);
                CurrentSoundLabel.Text = $"Playing: {_selectedSound}";
                CurrentSoundLabel.Foreground = new SolidColorBrush(Colors.Green);
                StopButton.IsEnabled = true;
                PlayButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to play sound: {ex.Message}");
                CurrentSoundLabel.Text = $"❌ Error: {ex.Message}";
                CurrentSoundLabel.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void OnStopClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                _audioService.Stop();
                CurrentSoundLabel.Text = "";
                StopButton.IsEnabled = false;
                PlayButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to stop sound: {ex.Message}");
            }
        }

        /// <summary>
        /// Find a sound file by searching all configured paths and categories.
        /// </summary>
        private string? FindSoundFile(string filename)
        {
            var categories = new[] { "ambient", "dialog", "music", "soundset", "amb", "dlg", "mus", "sts" };
            var basePaths = new List<string>();

            // Add user Documents path
            var userPath = SettingsService.Instance.NeverwinterNightsPath;
            if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
            {
                basePaths.Add(userPath);
            }

            // Add game installation path + data subdirectory
            var installPath = SettingsService.Instance.BaseGameInstallPath;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                basePaths.Add(installPath);

                var dataPath = Path.Combine(installPath, "data");
                if (Directory.Exists(dataPath))
                {
                    basePaths.Add(dataPath);
                }
            }

            // Search all combinations
            foreach (var basePath in basePaths)
            {
                foreach (var category in categories)
                {
                    var soundPath = Path.Combine(basePath, category, filename);
                    if (File.Exists(soundPath))
                    {
                        return soundPath;
                    }
                }
            }

            return null;
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedSound != null)
            {
                _soundService.AddRecentSound(_selectedSound);

                // NWN expects sound names WITHOUT file extension
                var soundNameWithoutExtension = Path.GetFileNameWithoutExtension(_selectedSound);
                Close(soundNameWithoutExtension);
                return;
            }
            Close(null);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
