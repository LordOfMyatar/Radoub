using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DialogEditor.Models.Sound;
using DialogEditor.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.UI.Services;

namespace DialogEditor.Views
{
    public partial class SoundBrowserWindow : Window
    {
        private readonly SoundService _soundService;
        private readonly AudioService _audioService;
        private readonly SoundScanner _scanner;
        private readonly SoundExtractor _extractor;
        private readonly IGameDataService? _gameDataService;
        private List<SoundFileInfo> _allSounds;
        private List<SoundFileInfo> _filteredSounds;
        private string? _selectedSound;
        private readonly string? _dialogFilePath;
        private SoundFileInfo? _selectedSoundInfo;
        private string? _overridePath;
        private bool _isInitializing = true;

        public string? SelectedSound => _selectedSound;

        // Theme-aware brush helpers
        private IBrush ForegroundBrush => GetResourceBrush("SystemControlForegroundBaseHighBrush") ?? Brushes.White;
        private IBrush SecondaryBrush => GetResourceBrush("SystemControlForegroundBaseMediumBrush") ?? Brushes.Gray;
        private static IBrush WarningBrush => new SolidColorBrush(Color.Parse("#FFA500"));
        private static IBrush ErrorBrush => new SolidColorBrush(Color.Parse("#FF4444"));
        private static IBrush SuccessBrush => new SolidColorBrush(Color.Parse("#44AA44"));
        private static IBrush HakBrush => new SolidColorBrush(Color.Parse("#6699FF"));

        private IBrush? GetResourceBrush(string key)
        {
            if (this.TryFindResource(key, out var resource) && resource is IBrush brush)
                return brush;
            return null;
        }

        public SoundBrowserWindow() : this(null, null) { }

        public SoundBrowserWindow(string? dialogFilePath, IGameDataService? gameDataService = null)
        {
            InitializeComponent();
            _soundService = new SoundService(SettingsService.Instance);
            _audioService = new AudioService();
            _scanner = new SoundScanner(SettingsService.Instance);
            _extractor = new SoundExtractor();
            _gameDataService = gameDataService;
            _allSounds = new List<SoundFileInfo>();
            _filteredSounds = new List<SoundFileInfo>();
            _dialogFilePath = dialogFilePath;

            _isInitializing = true;
            IncludeGameResourcesCheckBox.IsChecked = SettingsService.Instance.SoundBrowserIncludeGameResources;
            IncludeHakFolderCheckBox.IsChecked = SettingsService.Instance.SoundBrowserIncludeHakFiles;
            IncludeBifFilesCheckBox.IsChecked = SettingsService.Instance.SoundBrowserIncludeBifFiles;
            _isInitializing = false;

            _audioService.PlaybackStopped += OnPlaybackStopped;
            LoadSounds();

            Closing += (s, e) =>
            {
                _audioService.PlaybackStopped -= OnPlaybackStopped;
                _audioService.Dispose();
                _extractor.CleanupTempFile();
            };
        }

        private void OnPlaybackStopped(object? sender, EventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentSoundLabel.Text = "";
                StopButton.IsEnabled = false;
                PlayButton.IsEnabled = _selectedSoundInfo != null;
            });
        }

        private void OnSourceSelectionChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            SettingsService.Instance.SoundBrowserIncludeGameResources = IncludeGameResourcesCheckBox?.IsChecked == true;
            SettingsService.Instance.SoundBrowserIncludeHakFiles = IncludeHakFolderCheckBox?.IsChecked == true;
            SettingsService.Instance.SoundBrowserIncludeBifFiles = IncludeBifFilesCheckBox?.IsChecked == true;

            LoadSounds();
        }

        private void OnOtherLocationToggled(object? sender, RoutedEventArgs e)
        {
            var isEnabled = IncludeOtherLocationCheckBox?.IsChecked == true;
            BrowseLocationButton.IsEnabled = isEnabled;

            if (!isEnabled)
            {
                _overridePath = null;
                OtherLocationLabel.Text = "";
            }

            LoadSounds();
        }

        private async void OnBrowseLocationClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                IStorageFolder? suggestedStart = null;
                var basePath = SettingsService.Instance.BaseGameInstallPath;
                if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
                    suggestedStart = await StorageProvider.TryGetFolderFromPathAsync(basePath);

                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Sound Location",
                    AllowMultiple = false,
                    SuggestedStartLocation = suggestedStart
                });

                if (folders.Count > 0)
                {
                    _overridePath = folders[0].Path.LocalPath;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Sound browser: Other location set to {UnifiedLogger.SanitizePath(_overridePath)}");
                    OtherLocationLabel.Text = UnifiedLogger.SanitizePath(_overridePath);
                    LoadSounds();
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error selecting folder: {ex.Message}");
            }
        }

        private void OnMonoFilterChanged(object? sender, RoutedEventArgs e) => UpdateSoundList();

        private async void LoadSounds() => await LoadSoundsAsync();

        private async Task LoadSoundsAsync()
        {
            try
            {
                _allSounds = new List<SoundFileInfo>();
                FileCountLabel.Text = "Loading sounds...";
                FileCountLabel.Foreground = SecondaryBrush;

                var includeGameResources = IncludeGameResourcesCheckBox?.IsChecked == true;
                var includeHakFiles = IncludeHakFolderCheckBox?.IsChecked == true;
                var includeBifFiles = IncludeBifFilesCheckBox?.IsChecked == true;
                var includeOtherLocation = IncludeOtherLocationCheckBox?.IsChecked == true;

                if (!includeGameResources && !includeHakFiles && !includeBifFiles && !includeOtherLocation)
                {
                    FileCountLabel.Text = "Select at least one source";
                    FileCountLabel.Foreground = WarningBrush;
                    UpdateSoundList();
                    return;
                }

                var settings = SettingsService.Instance;

                // 1. Override folder and loose files (highest priority)
                if (includeGameResources)
                    await ScanGameResourcesAsync(settings);

                // 2. HAK files
                if (includeHakFiles)
                    await ScanHakFilesAsync(settings);

                // 3. BIF archives
                if (includeBifFiles)
                    await ScanBifArchivesAsync(settings);

                // 4. Other location
                if (includeOtherLocation && !string.IsNullOrEmpty(_overridePath))
                {
                    _allSounds.AddRange(_scanner.ScanPathForSounds(_overridePath, "Other", _allSounds));
                    var hakSounds = await _scanner.ScanPathForHaksAsync(_overridePath, _allSounds,
                        (name, current, total) => FileCountLabel.Text = $"Loading HAK {current}/{total}: {name}...");
                    _allSounds.AddRange(hakSounds);
                }

                // 5. Module-configured resources via IGameDataService (#1001)
                // Adds sounds from module HAKs that may not be in standard scan paths
                if (includeHakFiles || includeBifFiles)
                {
                    FileCountLabel.Text = "Checking module resources...";
                    LoadSoundsFromGameDataService();
                }

                if (_allSounds.Count == 0)
                {
                    var msg = GetNoSoundsMessage(includeGameResources, includeOtherLocation, settings);
                    FileCountLabel.Text = $"⚠ {msg}";
                    FileCountLabel.Foreground = WarningBrush;
                }

                UpdateSoundList();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load sounds: {ex.Message}");
                FileCountLabel.Text = $"❌ Error loading sounds: {ex.Message}";
                FileCountLabel.Foreground = ErrorBrush;
            }
        }

        private async Task ScanGameResourcesAsync(SettingsService settings)
        {
            var basePath = settings.BaseGameInstallPath;
            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                return;

            var userPath = settings.NeverwinterNightsPath;
            if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
            {
                var overrideFolder = Path.Combine(userPath, "override");
                if (Directory.Exists(overrideFolder))
                    _allSounds.AddRange(_scanner.ScanPathForSounds(overrideFolder, "Override", _allSounds));

                var ovrFolder = Path.Combine(userPath, "ovr");
                if (Directory.Exists(ovrFolder))
                    _allSounds.AddRange(_scanner.ScanPathForSounds(ovrFolder, "Override", _allSounds));

                _allSounds.AddRange(_scanner.ScanAllSoundFolders(userPath, _allSounds));
            }

            _allSounds.AddRange(_scanner.ScanAllSoundFolders(basePath, _allSounds));

            var dataPath = Path.Combine(basePath, "data");
            if (Directory.Exists(dataPath))
                _allSounds.AddRange(_scanner.ScanAllSoundFolders(dataPath, _allSounds));

            var langPath = Path.Combine(basePath, "lang");
            if (Directory.Exists(langPath))
            {
                foreach (var langDir in Directory.GetDirectories(langPath))
                {
                    var langDataPath = Path.Combine(langDir, "data");
                    if (Directory.Exists(langDataPath))
                        _allSounds.AddRange(_scanner.ScanAllSoundFolders(langDataPath, _allSounds));
                }
            }

            await Task.CompletedTask;
        }

        private async Task ScanHakFilesAsync(SettingsService settings)
        {
            if (!string.IsNullOrEmpty(_dialogFilePath))
            {
                var dialogDir = Path.GetDirectoryName(_dialogFilePath);
                if (!string.IsNullOrEmpty(dialogDir) && Directory.Exists(dialogDir))
                {
                    var hakSounds = await _scanner.ScanPathForHaksAsync(dialogDir, _allSounds,
                        (name, current, total) => FileCountLabel.Text = $"Loading HAK {current}/{total}: {name}...");
                    _allSounds.AddRange(hakSounds);
                }
            }

            var userPath = settings.NeverwinterNightsPath;
            if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
            {
                var hakFolder = Path.Combine(userPath, "hak");
                if (Directory.Exists(hakFolder))
                {
                    var hakSounds = await _scanner.ScanPathForHaksAsync(hakFolder, _allSounds,
                        (name, current, total) => FileCountLabel.Text = $"Loading HAK {current}/{total}: {name}...");
                    _allSounds.AddRange(hakSounds);
                }
            }

            var basePath = settings.BaseGameInstallPath;
            if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
            {
                var dataPath = Path.Combine(basePath, "data");
                if (Directory.Exists(dataPath))
                {
                    var hakSounds = await _scanner.ScanPathForHaksAsync(dataPath, _allSounds,
                        (name, current, total) => FileCountLabel.Text = $"Loading HAK {current}/{total}: {name}...");
                    _allSounds.AddRange(hakSounds);
                }

                var langPath = Path.Combine(basePath, "lang");
                if (Directory.Exists(langPath))
                {
                    foreach (var langDir in Directory.GetDirectories(langPath))
                    {
                        var langDataPath = Path.Combine(langDir, "data");
                        if (Directory.Exists(langDataPath))
                        {
                            var hakSounds = await _scanner.ScanPathForHaksAsync(langDataPath, _allSounds,
                                (name, current, total) => FileCountLabel.Text = $"Loading HAK {current}/{total}: {name}...");
                            _allSounds.AddRange(hakSounds);
                        }
                    }
                }
            }
        }

        private async Task ScanBifArchivesAsync(SettingsService settings)
        {
            var basePath = settings.BaseGameInstallPath;
            if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
            {
                FileCountLabel.Text = "Loading base game sounds from BIF archives...";
                var bifSounds = await _scanner.ScanBifArchivesAsync(basePath, _allSounds,
                    bifName => FileCountLabel.Text = $"Loading sounds from {bifName}...");
                _allSounds.AddRange(bifSounds);
            }
        }

        /// <summary>
        /// Load sounds from IGameDataService (module-aware resolution).
        /// Adds any sounds from module HAKs not already found by direct scanning.
        /// </summary>
        private void LoadSoundsFromGameDataService()
        {
            if (_gameDataService == null || !_gameDataService.IsConfigured)
                return;

            try
            {
                var existingResRefs = new HashSet<string>(
                    _allSounds.Select(s => Path.GetFileNameWithoutExtension(s.FileName)),
                    StringComparer.OrdinalIgnoreCase);

                var resources = _gameDataService.ListResources(ResourceTypes.Wav);
                foreach (var resource in resources)
                {
                    // Skip if already loaded from another source
                    if (existingResRefs.Contains(resource.ResRef))
                        continue;

                    // Create SoundFileInfo for this resource
                    // Note: IsFromHak/IsFromBif are computed properties based on HakPath+ErfEntry/BifInfo
                    // For IGameDataService resources, we set HakPath without ErfEntry (extraction via service)
                    var soundInfo = new SoundFileInfo
                    {
                        FileName = resource.ResRef + ".wav",
                        FullPath = resource.Source == GameResourceSource.Override ? (resource.SourcePath ?? "") : "",
                        IsMono = true, // Assume mono (NWN standard), will validate on selection
                        ChannelUnknown = true, // Haven't validated yet
                        Source = GetSourceLabel(resource.Source),
                        // For HAK/BIF sounds from IGameDataService, store path for reference
                        // but don't set ErfEntry/BifInfo (extraction handled differently)
                        HakPath = resource.Source == GameResourceSource.Hak ? resource.SourcePath : null
                    };

                    _allSounds.Add(soundInfo);
                    existingResRefs.Add(resource.ResRef);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Error loading sounds from IGameDataService: {ex.Message}");
            }
        }

        private static string GetSourceLabel(GameResourceSource source)
        {
            return source switch
            {
                GameResourceSource.Override => "Override",
                GameResourceSource.Hak => "HAK (Module)",
                GameResourceSource.Module => "Module",
                GameResourceSource.Bif => "BIF",
                _ => source.ToString()
            };
        }

        private static string GetNoSoundsMessage(bool includeGameResources, bool includeOtherLocation, SettingsService settings)
        {
            if (!includeGameResources && string.IsNullOrEmpty(settings.BaseGameInstallPath))
                return "Configure game path in Settings, or use Other location";
            if (includeOtherLocation)
                return "Click browse... to select a folder";
            return "No sounds found";
        }

        private void UpdateSoundList()
        {
            SoundListBox.Items.Clear();

            var soundsToDisplay = _allSounds.ToList();
            var monoOnly = MonoOnlyCheckBox?.IsChecked == true;
            if (monoOnly)
            {
                // Include sounds that are mono OR have unknown channel status (archive sounds not yet validated)
                soundsToDisplay = soundsToDisplay.Where(s => s.IsMono || s.ChannelUnknown).ToList();
            }

            var searchText = SearchBox?.Text?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(searchText))
                soundsToDisplay = soundsToDisplay.Where(s => s.FileName.ToLowerInvariant().Contains(searchText)).ToList();

            soundsToDisplay = soundsToDisplay.OrderBy(s => s.FileName).ToList();
            _filteredSounds = soundsToDisplay;

            foreach (var sound in soundsToDisplay)
            {
                var (displayName, foreground) = GetSoundDisplayInfo(sound);
                SoundListBox.Items.Add(new ListBoxItem { Content = displayName, Tag = sound, Foreground = foreground });
            }

            UpdateFileCountLabel(soundsToDisplay.Count, monoOnly);
        }

        private (string displayName, IBrush foreground) GetSoundDisplayInfo(SoundFileInfo sound)
        {
            var baseName = sound.FileName;
            var sourceInfo = !string.IsNullOrEmpty(sound.Source) ? $" ({sound.Source})" : "";

            if (!sound.IsValidWav)
                return ($"❌ {baseName}{sourceInfo} [invalid]", ErrorBrush);
            if (!sound.IsMono && !sound.ChannelUnknown)
                return ($"⚠️ {baseName}{sourceInfo} [stereo]", WarningBrush);
            if (sound.IsFromHak)
            {
                var channelHint = sound.ChannelUnknown ? " [?ch]" : "";
                return ($"📦 {baseName}{sourceInfo}{channelHint}", HakBrush);
            }
            if (sound.IsFromBif)
            {
                var channelHint = sound.ChannelUnknown ? " [?ch]" : "";
                return ($"🎮 {baseName}{sourceInfo}{channelHint}", ForegroundBrush);
            }
            return ($"{baseName}{sourceInfo}", ForegroundBrush);
        }

        private void UpdateFileCountLabel(int count, bool monoOnly)
        {
            var hakCount = _filteredSounds.Count(s => s.IsFromHak);
            var bifCount = _filteredSounds.Count(s => s.IsFromBif);
            var stereoCount = _filteredSounds.Count(s => !s.IsMono && !s.ChannelUnknown);
            var unknownChannelCount = _filteredSounds.Count(s => s.ChannelUnknown);
            var invalidCount = _filteredSounds.Count(s => !s.IsValidWav);
            var countText = $"{count} sound{(count == 1 ? "" : "s")}";

            var details = new List<string>();
            if (bifCount > 0) details.Add($"{bifCount} from BIF");
            if (hakCount > 0) details.Add($"{hakCount} from HAK");
            if (!monoOnly && stereoCount > 0) details.Add($"{stereoCount} stereo");
            if (monoOnly && unknownChannelCount > 0) details.Add($"{unknownChannelCount} unverified");
            if (invalidCount > 0) details.Add($"{invalidCount} invalid");

            if (details.Count > 0)
                countText += $" ({string.Join(", ", details)})";

            FileCountLabel.Text = countText;
            FileCountLabel.Foreground = ForegroundBrush;
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => UpdateSoundList();

        private void OnSoundSelected(object? sender, SelectionChangedEventArgs e)
        {
            SoundFileInfo? soundInfo = null;
            if (SoundListBox.SelectedItem is ListBoxItem listBoxItem && listBoxItem.Tag is SoundFileInfo info)
                soundInfo = info;

            _selectedSoundInfo = soundInfo;

            if (soundInfo != null)
            {
                _selectedSound = soundInfo.FileName;
                SelectedSoundLabel.Text = soundInfo.IsFromHak ? $"{soundInfo.FileName} 📦"
                    : soundInfo.IsFromBif ? $"{soundInfo.FileName} 🎮"
                    : soundInfo.FileName;
                PlayButton.IsEnabled = true;

                FileCountLabel.Text = "Validating...";
                FileCountLabel.Foreground = SecondaryBrush;
                CurrentSoundLabel.Text = "";

                var skipValidation = DisableValidationCheckBox?.IsChecked == true;
                ValidateSelectedSound(soundInfo, skipValidation);
            }
            else
            {
                _selectedSound = null;
                _selectedSoundInfo = null;
                SelectedSoundLabel.Text = "(none)";
                PlayButton.IsEnabled = false;
            }
        }

        private void ValidateSelectedSound(SoundFileInfo soundInfo, bool skipValidation)
        {
            try
            {
                if (soundInfo.IsFromHak)
                {
                    if (skipValidation)
                    {
                        FileCountLabel.Text = $"📦 From: {soundInfo.Source}";
                        FileCountLabel.Foreground = HakBrush;
                    }
                    else
                    {
                        var result = _extractor.ValidateHakSound(soundInfo);
                        DisplayValidationResult(result);
                    }
                }
                else if (soundInfo.IsFromBif)
                {
                    if (skipValidation)
                    {
                        FileCountLabel.Text = $"🎮 From: {soundInfo.Source}";
                        FileCountLabel.Foreground = ForegroundBrush;
                    }
                    else
                    {
                        var result = _extractor.ValidateBifSound(soundInfo);
                        DisplayValidationResult(result);
                    }
                }
                else
                {
                    var validation = SoundValidator.Validate(soundInfo.FullPath, isVoiceOrSfx: true);
                    if (validation.HasIssues)
                    {
                        var issues = string.Join(", ", validation.Errors.Concat(validation.Warnings));
                        FileCountLabel.Text = validation.IsValid ? $"⚠ {issues}" : $"❌ {issues}";
                        FileCountLabel.Foreground = validation.IsValid ? WarningBrush : ErrorBrush;
                    }
                    else if (!string.IsNullOrEmpty(validation.FormatInfo))
                    {
                        FileCountLabel.Text = $"✓ {validation.FormatInfo}";
                        FileCountLabel.Foreground = SuccessBrush;
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Validation error: {ex.Message}");
            }
        }

        private void DisplayValidationResult(ArchiveSoundValidationResult result)
        {
            var sourceIcon = result.IsFromHak ? "📦" : "🎮";
            var sourceInfo = $" ({sourceIcon} {result.Source})";

            if (result.ValidationUnavailable)
            {
                FileCountLabel.Text = $"{sourceIcon} From: {result.Source} (validation unavailable)";
                FileCountLabel.Foreground = result.IsFromHak ? HakBrush : ForegroundBrush;
            }
            else if (result.ExtractionFailed)
            {
                FileCountLabel.Text = $"{sourceIcon} From: {result.Source} (extraction failed)";
                FileCountLabel.Foreground = WarningBrush;
            }
            else if (!result.IsValidWav)
            {
                FileCountLabel.Text = $"⚠️ {result.InvalidWavReason}{sourceInfo} - playback may fail";
                FileCountLabel.Foreground = WarningBrush;
            }
            else if (result.HasIssues)
            {
                var issues = string.Join(", ", result.Errors.Concat(result.Warnings));
                FileCountLabel.Text = result.IsValid ? $"⚠ {issues}{sourceInfo}" : $"❌ {issues}{sourceInfo}";
                FileCountLabel.Foreground = result.IsValid ? WarningBrush : ErrorBrush;
            }
            else if (!string.IsNullOrEmpty(result.FormatInfo))
            {
                FileCountLabel.Text = $"✓ {result.FormatInfo}{sourceInfo}";
                FileCountLabel.Foreground = SuccessBrush;
            }
            else
            {
                FileCountLabel.Text = $"{sourceIcon} From: {result.Source}";
                FileCountLabel.Foreground = result.IsFromHak ? HakBrush : ForegroundBrush;
            }
        }

        private async void OnSoundDoubleClicked(object? sender, RoutedEventArgs e)
        {
            if (_selectedSound != null)
            {
                // #827: Warn if stereo file selected
                if (_selectedSoundInfo != null && !_selectedSoundInfo.IsMono)
                {
                    var proceed = await ShowStereoWarningAsync(_selectedSoundInfo.FileName);
                    if (!proceed) return;
                }

                _soundService.AddRecentSound(_selectedSound);
                Close(Path.GetFileNameWithoutExtension(_selectedSound));
            }
        }

        private void OnPlayClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedSound == null || _selectedSoundInfo == null)
                return;

            try
            {
                string? pathToPlay = null;

                if (_selectedSoundInfo.IsFromHak)
                {
                    pathToPlay = _extractor.ExtractHakSoundToTemp(_selectedSoundInfo);
                    if (pathToPlay == null)
                    {
                        CurrentSoundLabel.Text = "❌ Failed to extract from HAK";
                        CurrentSoundLabel.Foreground = ErrorBrush;
                        return;
                    }
                }
                else if (_selectedSoundInfo.IsFromBif)
                {
                    pathToPlay = _extractor.ExtractBifSoundToTemp(_selectedSoundInfo);
                    if (pathToPlay == null)
                    {
                        CurrentSoundLabel.Text = "❌ Failed to extract from BIF";
                        CurrentSoundLabel.Foreground = ErrorBrush;
                        return;
                    }
                }
                else
                {
                    pathToPlay = _selectedSoundInfo.FullPath;
                    if (!File.Exists(pathToPlay))
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound file not found: {_selectedSound}");
                        CurrentSoundLabel.Text = $"⚠ File not found: {_selectedSound}";
                        CurrentSoundLabel.Foreground = WarningBrush;
                        return;
                    }
                }

                var invalidWarning = !_selectedSoundInfo.IsValidWav ? " ⚠️ (invalid format)" : "";
                var sourceLabel = _selectedSoundInfo.IsFromHak ? " (from HAK)"
                    : _selectedSoundInfo.IsFromBif ? " (from BIF)" : "";

                _audioService.Play(pathToPlay);
                CurrentSoundLabel.Text = $"Playing: {_selectedSound}{sourceLabel}{invalidWarning}";
                CurrentSoundLabel.Foreground = !_selectedSoundInfo.IsValidWav ? WarningBrush : SuccessBrush;
                StopButton.IsEnabled = true;
                PlayButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to play sound: {ex.Message}");
                var hint = !_selectedSoundInfo.IsValidWav ? $" ({_selectedSoundInfo.InvalidWavReason})" : "";
                CurrentSoundLabel.Text = $"❌ Error: {ex.Message}{hint}";
                CurrentSoundLabel.Foreground = ErrorBrush;
                PlayButton.IsEnabled = true;
                StopButton.IsEnabled = false;
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

        private async void OnOkClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedSound != null)
            {
                // #827: Warn if stereo file selected
                if (_selectedSoundInfo != null && !_selectedSoundInfo.IsMono)
                {
                    var proceed = await ShowStereoWarningAsync(_selectedSoundInfo.FileName);
                    if (!proceed) return;
                }

                _soundService.AddRecentSound(_selectedSound);
                Close(Path.GetFileNameWithoutExtension(_selectedSound));
                return;
            }
            Close(null);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

        #region Stereo Warning (#827)

        /// <summary>
        /// Shows warning dialog when user selects a stereo sound file.
        /// Returns true if user wants to proceed, false to cancel.
        /// </summary>
        private async Task<bool> ShowStereoWarningAsync(string fileName)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Stereo sound selected: '{fileName}' - showing confirmation dialog");

            var dialog = new Window
            {
                Title = "Stereo Sound Warning",
                Width = 450,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = $"Sound file '{fileName}' is stereo.\n\n" +
                       "NWN conversation audio requires mono files.\n" +
                       "Stereo sounds may play incorrectly in-game.\n\n" +
                       "Assign anyway?",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            var result = false;

            var yesButton = new Button { Content = "Assign Anyway", Width = 120 };
            yesButton.Click += (s, args) =>
            {
                result = true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"User chose to assign stereo sound: '{fileName}'");
                dialog.Close();
            };

            var noButton = new Button { Content = "Cancel", Width = 80 };
            noButton.Click += (s, args) =>
            {
                result = false;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"User cancelled stereo sound assignment: '{fileName}'");
                dialog.Close();
            };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(this);
            return result;
        }

        #endregion

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
    }
}
