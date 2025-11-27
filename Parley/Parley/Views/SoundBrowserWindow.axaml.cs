using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DialogEditor.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;

namespace DialogEditor.Views
{
    /// <summary>
    /// Sound info with path/source and mono status for filtering.
    /// </summary>
    public class SoundFileInfo
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsMono { get; set; } = true;

        /// <summary>
        /// Source of the sound (e.g., "Override", "customsounds.hak", "Base Game").
        /// </summary>
        public string Source { get; set; } = "";

        /// <summary>
        /// If from HAK, the path to the HAK file.
        /// </summary>
        public string? HakPath { get; set; }

        /// <summary>
        /// If from HAK, the ERF resource entry for extraction.
        /// </summary>
        public ErfResourceEntry? ErfEntry { get; set; }

        /// <summary>
        /// True if this sound comes from a HAK file (requires extraction for playback).
        /// </summary>
        public bool IsFromHak => HakPath != null && ErfEntry != null;
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
        private SoundFileInfo? _selectedSoundInfo;
        private string? _tempExtractedPath; // For HAK sounds extracted for playback

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

            // Clean up audio and temp files on window close
            Closing += (s, e) =>
            {
                _audioService.Dispose();
                CleanupTempFile();
            };
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
                    // Override mode: scan custom path for all sounds and HAKs
                    ScanPathForSounds(_overridePath, "Override");
                    ScanPathForHaks(_overridePath);
                }
                else
                {
                    // Default: use game paths with NWN resource priority
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

                    // NWN Resource Priority:
                    // 1. Override folder (highest priority)
                    var userPath = SettingsService.Instance.NeverwinterNightsPath;
                    if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
                    {
                        var overrideFolder = Path.Combine(userPath, "override");
                        if (Directory.Exists(overrideFolder))
                        {
                            ScanPathForSounds(overrideFolder, "Override");
                        }
                        ScanAllSoundFolders(userPath);
                    }

                    // 2. HAK files (scan dialog directory for module-specific HAKs)
                    if (!string.IsNullOrEmpty(_dialogFilePath))
                    {
                        var dialogDir = Path.GetDirectoryName(_dialogFilePath);
                        if (!string.IsNullOrEmpty(dialogDir) && Directory.Exists(dialogDir))
                        {
                            ScanPathForHaks(dialogDir);
                        }
                    }

                    // 3. Base game resources
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

        private void ScanPathForHaks(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return;

                var hakFiles = Directory.GetFiles(path, "*.hak", SearchOption.TopDirectoryOnly);
                foreach (var hakFile in hakFiles)
                {
                    ScanHakForSounds(hakFile);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning for HAKs in {path}: {ex.Message}");
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

        private void ScanPathForSounds(string path, string source = "")
        {
            try
            {
                var wavFiles = Directory.GetFiles(path, "*.wav", SearchOption.TopDirectoryOnly);
                foreach (var file in wavFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var isMono = SoundValidator.IsMonoWav(file);

                    // Check if already exists (avoid duplicates - first found wins, per NWN resource priority)
                    if (!_allSounds.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _allSounds.Add(new SoundFileInfo
                        {
                            FileName = fileName,
                            FullPath = file,
                            IsMono = isMono,
                            Source = string.IsNullOrEmpty(source) ? Path.GetFileName(path) : source
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

        private void ScanHakForSounds(string hakPath)
        {
            try
            {
                var erf = ErfReader.Read(hakPath);
                var hakFileName = Path.GetFileName(hakPath);

                // Get all WAV resources
                var wavResources = erf.GetResourcesByType(ResourceTypes.Wav);

                foreach (var resource in wavResources)
                {
                    var fileName = $"{resource.ResRef}.wav";

                    // Check if already exists (avoid duplicates - first found wins)
                    if (!_allSounds.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        // For HAK sounds, we can't easily check mono without extraction
                        // Mark as unknown (true) - user can verify on selection
                        _allSounds.Add(new SoundFileInfo
                        {
                            FileName = fileName,
                            FullPath = hakPath, // Store HAK path for extraction
                            IsMono = true, // Assume mono until verified
                            Source = hakFileName,
                            HakPath = hakPath,
                            ErfEntry = resource
                        });
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Sound Browser: Found {wavResources.Count()} WAV resources in {hakFileName}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {hakPath}: {ex.Message}");
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
                // Build display name with source and stereo indicator
                var baseName = sound.FileName;
                var sourceInfo = !string.IsNullOrEmpty(sound.Source) ? $" ({sound.Source})" : "";

                string displayName;
                IBrush foreground;

                if (!sound.IsMono)
                {
                    displayName = $"⚠️ {baseName}{sourceInfo} [stereo]";
                    foreground = new SolidColorBrush(Colors.Orange);
                }
                else if (sound.IsFromHak)
                {
                    displayName = $"📦 {baseName}{sourceInfo}";
                    foreground = new SolidColorBrush(Colors.LightBlue);
                }
                else
                {
                    displayName = $"{baseName}{sourceInfo}";
                    foreground = new SolidColorBrush(Colors.White);
                }

                SoundListBox.Items.Add(new ListBoxItem
                {
                    Content = displayName,
                    Tag = sound,
                    Foreground = foreground
                });
            }

            // Build count text with HAK count
            var hakCount = _filteredSounds.Count(s => s.IsFromHak);
            var stereoCount = _filteredSounds.Count(s => !s.IsMono);
            var countText = $"{soundsToDisplay.Count} sound{(soundsToDisplay.Count == 1 ? "" : "s")}";

            var details = new List<string>();
            if (hakCount > 0)
                details.Add($"{hakCount} from HAK");
            if (!monoOnly && stereoCount > 0)
                details.Add($"{stereoCount} stereo");

            if (details.Count > 0)
                countText += $" ({string.Join(", ", details)})";

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

            _selectedSoundInfo = soundInfo;

            if (soundInfo != null)
            {
                _selectedSound = soundInfo.FileName;
                _selectedSoundPath = soundInfo.IsFromHak ? null : soundInfo.FullPath;
                SelectedSoundLabel.Text = soundInfo.IsFromHak
                    ? $"{soundInfo.FileName} 📦"
                    : soundInfo.FileName;
                PlayButton.IsEnabled = true;

                // Validate sound file format against NWN specs
                try
                {
                    if (soundInfo.IsFromHak)
                    {
                        // For HAK sounds, extract temporarily to validate format
                        ValidateHakSound(soundInfo);
                    }
                    else
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
                _selectedSoundInfo = null;
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
            if (_selectedSound == null || _selectedSoundInfo == null)
                return;

            try
            {
                string pathToPlay;

                if (_selectedSoundInfo.IsFromHak)
                {
                    // Extract from HAK to temp file for playback
                    var extractedPath = ExtractHakSoundToTemp(_selectedSoundInfo);
                    if (extractedPath == null)
                    {
                        CurrentSoundLabel.Text = $"❌ Failed to extract from HAK";
                        CurrentSoundLabel.Foreground = new SolidColorBrush(Colors.Red);
                        return;
                    }
                    pathToPlay = extractedPath;
                }
                else
                {
                    pathToPlay = _selectedSoundInfo.FullPath;
                    if (!File.Exists(pathToPlay))
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound file not found: {_selectedSound}");
                        CurrentSoundLabel.Text = $"⚠ File not found: {_selectedSound}";
                        CurrentSoundLabel.Foreground = new SolidColorBrush(Colors.Orange);
                        return;
                    }
                }

                _audioService.Play(pathToPlay);
                CurrentSoundLabel.Text = _selectedSoundInfo.IsFromHak
                    ? $"Playing: {_selectedSound} (from HAK)"
                    : $"Playing: {_selectedSound}";
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

        private void ValidateHakSound(SoundFileInfo soundInfo)
        {
            if (soundInfo.HakPath == null || soundInfo.ErfEntry == null)
            {
                FileCountLabel.Text = $"📦 From: {soundInfo.Source}";
                FileCountLabel.Foreground = new SolidColorBrush(Colors.LightBlue);
                return;
            }

            try
            {
                // Extract sound data from HAK (in memory) to validate
                var soundData = ErfReader.ExtractResource(soundInfo.HakPath, soundInfo.ErfEntry);

                // Write to temp file for validation
                var tempPath = Path.Combine(Path.GetTempPath(), $"parley_validate_{soundInfo.ErfEntry.ResRef}.wav");
                File.WriteAllBytes(tempPath, soundData);

                try
                {
                    var validation = SoundValidator.Validate(tempPath, isVoiceOrSfx: true);

                    // Update the cached mono status
                    soundInfo.IsMono = validation.IsMono;

                    var sourceInfo = $" (📦 {soundInfo.Source})";
                    if (validation.HasIssues)
                    {
                        var issues = string.Join(", ", validation.Errors.Concat(validation.Warnings));
                        FileCountLabel.Text = validation.IsValid
                            ? $"⚠ {issues}{sourceInfo}"
                            : $"❌ {issues}{sourceInfo}";
                        FileCountLabel.Foreground = validation.IsValid
                            ? new SolidColorBrush(Colors.Orange)
                            : new SolidColorBrush(Colors.Red);
                    }
                    else if (!string.IsNullOrEmpty(validation.FormatInfo))
                    {
                        FileCountLabel.Text = $"✓ {validation.FormatInfo}{sourceInfo}";
                        FileCountLabel.Foreground = new SolidColorBrush(Colors.Green);
                    }
                    else
                    {
                        FileCountLabel.Text = $"📦 From: {soundInfo.Source}";
                        FileCountLabel.Foreground = new SolidColorBrush(Colors.LightBlue);
                    }
                }
                finally
                {
                    // Clean up temp validation file
                    try { File.Delete(tempPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not validate HAK sound: {ex.Message}");
                FileCountLabel.Text = $"📦 From: {soundInfo.Source} (validation unavailable)";
                FileCountLabel.Foreground = new SolidColorBrush(Colors.LightBlue);
            }
        }

        private string? ExtractHakSoundToTemp(SoundFileInfo soundInfo)
        {
            if (soundInfo.HakPath == null || soundInfo.ErfEntry == null)
                return null;

            try
            {
                // Clean up previous temp file
                CleanupTempFile();

                // Extract to temp directory
                var tempDir = Path.GetTempPath();
                var tempFileName = $"parley_sound_{soundInfo.ErfEntry.ResRef}.wav";
                var tempPath = Path.Combine(tempDir, tempFileName);

                var soundData = ErfReader.ExtractResource(soundInfo.HakPath, soundInfo.ErfEntry);
                File.WriteAllBytes(tempPath, soundData);

                _tempExtractedPath = tempPath;

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Extracted HAK sound to temp: {tempFileName}");

                return tempPath;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to extract HAK sound: {ex.Message}");
                return null;
            }
        }

        private void CleanupTempFile()
        {
            if (!string.IsNullOrEmpty(_tempExtractedPath) && File.Exists(_tempExtractedPath))
            {
                try
                {
                    File.Delete(_tempExtractedPath);
                    _tempExtractedPath = null;
                }
                catch
                {
                    // Ignore cleanup errors
                }
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
