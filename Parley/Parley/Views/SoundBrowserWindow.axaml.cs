using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DialogEditor.Services;

namespace DialogEditor.Views
{
    /// <summary>
    /// Sound info with path and mono status for filtering.
    /// </summary>
    public class SoundFileInfo
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsMono { get; set; } = true;
    }

    public partial class SoundBrowserWindow : Window
    {
        private readonly SoundService _soundService;
        private readonly AudioService _audioService;
        private List<SoundFileInfo> _allSounds;
        private List<SoundFileInfo> _filteredSounds;
        private string? _selectedSound;
        private string? _selectedSoundPath;
        private string? _overridePath;
        private readonly string? _dialogFilePath;

        public string? SelectedSound => _selectedSound;

        // Parameterless constructor for XAML designer/runtime loader
        public SoundBrowserWindow() : this(null)
        {
        }

        public SoundBrowserWindow(string? dialogFilePath)
        {
            InitializeComponent();
            _soundService = new SoundService(SettingsService.Instance);
            _audioService = new AudioService();
            _allSounds = new List<SoundFileInfo>();
            _filteredSounds = new List<SoundFileInfo>();
            _dialogFilePath = dialogFilePath;

            UpdateLocationDisplay();
            LoadSounds();

            // Clean up audio on window close
            Closing += (s, e) => _audioService.Dispose();
        }

        private void UpdateLocationDisplay()
        {
            if (!string.IsNullOrEmpty(_overridePath))
            {
                LocationPathLabel.Text = UnifiedLogger.SanitizePath(_overridePath);
                LocationPathLabel.Foreground = new SolidColorBrush(Colors.White);
                ResetLocationButton.IsVisible = true;
            }
            else
            {
                var basePath = SettingsService.Instance.BaseGameInstallPath;
                if (!string.IsNullOrEmpty(basePath))
                {
                    LocationPathLabel.Text = UnifiedLogger.SanitizePath(basePath);
                    LocationPathLabel.Foreground = new SolidColorBrush(Colors.LightGray);
                }
                else
                {
                    LocationPathLabel.Text = "(no game path configured - use browse...)";
                    LocationPathLabel.Foreground = new SolidColorBrush(Colors.Orange);
                }
                ResetLocationButton.IsVisible = false;
            }
        }

        private async void OnBrowseLocationClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                IStorageFolder? suggestedStart = null;
                var basePath = SettingsService.Instance.BaseGameInstallPath;
                if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
                {
                    suggestedStart = await StorageProvider.TryGetFolderFromPathAsync(basePath);
                }

                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Sound Location",
                    AllowMultiple = false,
                    SuggestedStartLocation = suggestedStart
                });

                if (folders.Count > 0)
                {
                    var folder = folders[0];
                    _overridePath = folder.Path.LocalPath;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Sound browser: Override path set to {UnifiedLogger.SanitizePath(_overridePath)}");

                    UpdateLocationDisplay();
                    LoadSounds();
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error selecting folder: {ex.Message}");
            }
        }

        private void OnResetLocationClick(object? sender, RoutedEventArgs e)
        {
            _overridePath = null;
            UnifiedLogger.LogApplication(LogLevel.INFO, "Sound browser: Reset to auto-detected paths");
            UpdateLocationDisplay();
            LoadSounds();
        }

        private void OnMonoFilterChanged(object? sender, RoutedEventArgs e)
        {
            UpdateSoundList();
        }

        private void LoadSounds()
        {
            try
            {
                _allSounds = new List<SoundFileInfo>();

                if (!string.IsNullOrEmpty(_overridePath))
                {
                    // Override mode: scan custom path for all sounds
                    ScanPathForSounds(_overridePath);
                }
                else
                {
                    // Default: use game paths
                    var basePath = SettingsService.Instance.BaseGameInstallPath;
                    if (string.IsNullOrEmpty(basePath))
                    {
                        FileCountLabel.Text = "⚠ Base game path not configured - use browse... or go to Settings";
                        FileCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
                        UnifiedLogger.LogApplication(LogLevel.WARN, "Sound Browser: Base game path not configured");
                        return;
                    }

                    if (!Directory.Exists(basePath))
                    {
                        FileCountLabel.Text = "⚠ Game path not found - use browse...";
                        FileCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound Browser: Game path does not exist: {UnifiedLogger.SanitizePath(basePath)}");
                        return;
                    }

                    // Scan user path
                    var userPath = SettingsService.Instance.NeverwinterNightsPath;
                    if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
                    {
                        ScanAllSoundFolders(userPath);
                    }

                    // Scan game installation
                    ScanAllSoundFolders(basePath);
                    var dataPath = Path.Combine(basePath, "data");
                    if (Directory.Exists(dataPath))
                    {
                        ScanAllSoundFolders(dataPath);
                    }
                }

                if (_allSounds.Count == 0)
                {
                    FileCountLabel.Text = "⚠ No sound files found. Use browse... to select a folder with .wav files.";
                    FileCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
                    UnifiedLogger.LogApplication(LogLevel.WARN, "No sound files found");
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

        private void ScanAllSoundFolders(string basePath)
        {
            // Scan known NWN sound folders
            var soundFolders = new[] { "ambient", "amb", "dialog", "dlg", "music", "mus", "soundset", "sts" };
            foreach (var folder in soundFolders)
            {
                var fullPath = Path.Combine(basePath, folder);
                if (Directory.Exists(fullPath))
                {
                    ScanPathForSounds(fullPath);
                }
            }
        }

        private void ScanPathForSounds(string path)
        {
            try
            {
                var wavFiles = Directory.GetFiles(path, "*.wav", SearchOption.TopDirectoryOnly);
                foreach (var file in wavFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var isMono = SoundValidator.IsMonoWav(file);

                    // Check if already exists (avoid duplicates)
                    if (!_allSounds.Any(s => s.FileName == fileName))
                    {
                        _allSounds.Add(new SoundFileInfo
                        {
                            FileName = fileName,
                            FullPath = file,
                            IsMono = isMono
                        });
                    }
                }

                // Don't scan for BMU files - they're music format, not usable in conversations
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning {path}: {ex.Message}");
            }
        }

        private void UpdateSoundList()
        {
            SoundListBox.Items.Clear();

            var soundsToDisplay = _allSounds.ToList();

            // Apply mono filter if checkbox is checked
            var monoOnly = MonoOnlyCheckBox?.IsChecked == true;
            if (monoOnly)
            {
                soundsToDisplay = soundsToDisplay.Where(s => s.IsMono).ToList();
            }

            // Apply search filter if active
            var searchText = SearchBox?.Text?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                soundsToDisplay = soundsToDisplay
                    .Where(s => s.FileName.ToLowerInvariant().Contains(searchText))
                    .ToList();
            }

            // Sort alphabetically
            soundsToDisplay = soundsToDisplay.OrderBy(s => s.FileName).ToList();

            _filteredSounds = soundsToDisplay;

            foreach (var sound in soundsToDisplay)
            {
                // Show stereo indicator if filter is off
                var displayName = sound.IsMono ? sound.FileName : $"⚠️ {sound.FileName} (stereo)";
                SoundListBox.Items.Add(new ListBoxItem
                {
                    Content = displayName,
                    Tag = sound,
                    Foreground = sound.IsMono
                        ? new SolidColorBrush(Colors.White)
                        : new SolidColorBrush(Colors.Orange)
                });
            }

            var stereoCount = _filteredSounds.Count(s => !s.IsMono);
            var countText = $"{soundsToDisplay.Count} sound{(soundsToDisplay.Count == 1 ? "" : "s")}";
            if (!monoOnly && stereoCount > 0)
            {
                countText += $" ({stereoCount} stereo)";
            }
            FileCountLabel.Text = countText;
            FileCountLabel.Foreground = new SolidColorBrush(Colors.White);
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSoundList();
        }

        private void OnSoundSelected(object? sender, SelectionChangedEventArgs e)
        {
            SoundFileInfo? soundInfo = null;

            // Handle both ListBoxItem (with Tag) and direct string selection
            if (SoundListBox.SelectedItem is ListBoxItem listBoxItem && listBoxItem.Tag is SoundFileInfo info)
            {
                soundInfo = info;
            }

            if (soundInfo != null)
            {
                _selectedSound = soundInfo.FileName;
                _selectedSoundPath = soundInfo.FullPath;
                SelectedSoundLabel.Text = soundInfo.FileName;
                PlayButton.IsEnabled = true;

                // Validate sound file format against NWN specs
                try
                {
                    var validation = SoundValidator.Validate(soundInfo.FullPath, isVoiceOrSfx: true);

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
            else
            {
                _selectedSound = null;
                _selectedSoundPath = null;
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
            if (_selectedSound == null || _selectedSoundPath == null)
                return;

            try
            {
                if (!File.Exists(_selectedSoundPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound file not found: {_selectedSound}");
                    CurrentSoundLabel.Text = $"⚠ File not found: {_selectedSound}";
                    CurrentSoundLabel.Foreground = new SolidColorBrush(Colors.Orange);
                    return;
                }

                _audioService.Play(_selectedSoundPath);
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
