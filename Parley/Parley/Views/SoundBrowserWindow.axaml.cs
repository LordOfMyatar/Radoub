using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DialogEditor.Services;
using Radoub.Formats.Bif;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Key;

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

        /// <summary>
        /// If from BIF, the BIF sound info for extraction.
        /// </summary>
        public BifSoundInfo? BifInfo { get; set; }

        /// <summary>
        /// True if this sound comes from a BIF file (requires extraction for playback).
        /// </summary>
        public bool IsFromBif => BifInfo != null;

        /// <summary>
        /// True if the file has a valid WAV header. False for invalid/corrupt files.
        /// </summary>
        public bool IsValidWav { get; set; } = true;

        /// <summary>
        /// If IsValidWav is false, describes why.
        /// </summary>
        public string InvalidWavReason { get; set; } = "";
    }

    /// <summary>
    /// Cached HAK file data to avoid re-scanning on each browser open.
    /// </summary>
    internal class HakCacheEntry
    {
        public string HakPath { get; set; } = "";
        public DateTime LastModified { get; set; }
        public List<SoundFileInfo> Sounds { get; set; } = new();
    }

    /// <summary>
    /// Sound info from a BIF file (requires KEY to map ResRef to BIF location).
    /// </summary>
    public class BifSoundInfo
    {
        public string ResRef { get; set; } = "";
        public string BifPath { get; set; } = "";
        public int VariableTableIndex { get; set; }
        public uint FileSize { get; set; }
    }

    /// <summary>
    /// Cached KEY file data to avoid re-parsing on each browser open.
    /// </summary>
    internal class KeyCacheEntry
    {
        public string KeyPath { get; set; } = "";
        public DateTime LastModified { get; set; }
        public KeyFile? KeyFile { get; set; }
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

        // Static cache for HAK file contents - persists across window instances
        private static readonly Dictionary<string, HakCacheEntry> _hakCache = new();

        // Static cache for KEY files - persists across window instances
        private static readonly Dictionary<string, KeyCacheEntry> _keyCache = new();

        // Static cache for loaded BIF files - persists across window instances
        private static readonly Dictionary<string, BifFile> _bifCache = new();

        public string? SelectedSound => _selectedSound;

        // Theme-aware brush helpers - use foreground/accent colors that work in both light and dark mode
        private IBrush ForegroundBrush => GetResourceBrush("SystemControlForegroundBaseHighBrush") ?? Brushes.White;
        private IBrush SecondaryBrush => GetResourceBrush("SystemControlForegroundBaseMediumBrush") ?? Brushes.Gray;
        private IBrush AccentBrush => GetResourceBrush("SystemAccentColor") ?? Brushes.DodgerBlue;
        private static IBrush WarningBrush => new SolidColorBrush(Color.Parse("#FFA500")); // Orange works in both themes
        private static IBrush ErrorBrush => new SolidColorBrush(Color.Parse("#FF4444")); // Red that's visible in both themes
        private static IBrush SuccessBrush => new SolidColorBrush(Color.Parse("#44AA44")); // Green that's visible in both themes
        private static IBrush HakBrush => new SolidColorBrush(Color.Parse("#6699FF")); // Light blue that's visible in both themes

        private IBrush? GetResourceBrush(string key)
        {
            if (this.TryFindResource(key, out var resource) && resource is IBrush brush)
                return brush;
            return null;
        }

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

            // Subscribe to playback stopped event to update UI when sound finishes
            _audioService.PlaybackStopped += OnPlaybackStopped;

            LoadSounds();

            // Clean up audio and temp files on window close
            Closing += (s, e) =>
            {
                _audioService.PlaybackStopped -= OnPlaybackStopped;
                _audioService.Dispose();
                CleanupTempFile();
            };
        }

        private void OnPlaybackStopped(object? sender, EventArgs e)
        {
            // Update UI on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentSoundLabel.Text = "";
                StopButton.IsEnabled = false;
                PlayButton.IsEnabled = _selectedSoundInfo != null;
            });
        }

        private void UpdateOtherLocationDisplay()
        {
            if (!string.IsNullOrEmpty(_overridePath))
            {
                OtherLocationLabel.Text = UnifiedLogger.SanitizePath(_overridePath);
            }
            else
            {
                OtherLocationLabel.Text = "";
            }
        }

        private void OnSourceSelectionChanged(object? sender, RoutedEventArgs e)
        {
            LoadSounds();
        }

        private void OnOtherLocationToggled(object? sender, RoutedEventArgs e)
        {
            var isEnabled = IncludeOtherLocationCheckBox?.IsChecked == true;
            BrowseLocationButton.IsEnabled = isEnabled;

            if (!isEnabled)
            {
                // Clear the other location when unchecked
                _overridePath = null;
                UpdateOtherLocationDisplay();
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
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Sound browser: Other location set to {UnifiedLogger.SanitizePath(_overridePath)}");

                    UpdateOtherLocationDisplay();
                    LoadSounds();
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error selecting folder: {ex.Message}");
            }
        }

        private void OnMonoFilterChanged(object? sender, RoutedEventArgs e)
        {
            UpdateSoundList();
        }

        private async void LoadSounds()
        {
            await LoadSoundsAsync();
        }

        private async Task LoadSoundsAsync()
        {
            try
            {
                _allSounds = new List<SoundFileInfo>();

                // Show loading indicator
                FileCountLabel.Text = "Loading sounds...";
                FileCountLabel.Foreground = SecondaryBrush;

                var includeGameResources = IncludeGameResourcesCheckBox?.IsChecked == true;
                var includeHakFiles = IncludeHakFolderCheckBox?.IsChecked == true;
                var includeBifFiles = IncludeBifFilesCheckBox?.IsChecked == true;
                var includeOtherLocation = IncludeOtherLocationCheckBox?.IsChecked == true;

                // Check if any source is selected
                if (!includeGameResources && !includeHakFiles && !includeBifFiles && !includeOtherLocation)
                {
                    FileCountLabel.Text = "Select at least one source";
                    FileCountLabel.Foreground = WarningBrush;
                    UpdateSoundList();
                    return;
                }

                // NWN Resource Priority Order (highest to lowest):
                // 1. Override folder (loose files)
                // 2. HAK files (module HAKs, then user hak folder)
                // 3. Base game BIF archives

                // 1. Override folder and loose files (highest priority)
                if (includeGameResources)
                {
                    var basePath = SettingsService.Instance.BaseGameInstallPath;
                    if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
                    {
                        // Override folder (highest priority)
                        var userPath = SettingsService.Instance.NeverwinterNightsPath;
                        if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
                        {
                            var overrideFolder = Path.Combine(userPath, "override");
                            if (Directory.Exists(overrideFolder))
                            {
                                ScanPathForSounds(overrideFolder, "Override");
                            }
                            // Also check NWN:EE "ovr" folder
                            var ovrFolder = Path.Combine(userPath, "ovr");
                            if (Directory.Exists(ovrFolder))
                            {
                                ScanPathForSounds(ovrFolder, "Override");
                            }
                            ScanAllSoundFolders(userPath);
                        }

                        // Base game loose files
                        ScanAllSoundFolders(basePath);
                        var dataPath = Path.Combine(basePath, "data");
                        if (Directory.Exists(dataPath))
                        {
                            ScanAllSoundFolders(dataPath);
                        }

                        // Scan language-specific data folders
                        var langPath = Path.Combine(basePath, "lang");
                        if (Directory.Exists(langPath))
                        {
                            foreach (var langDir in Directory.GetDirectories(langPath))
                            {
                                var langDataPath = Path.Combine(langDir, "data");
                                if (Directory.Exists(langDataPath))
                                {
                                    ScanAllSoundFolders(langDataPath);
                                }
                            }
                        }
                    }
                }

                // 2. HAK files (higher priority than BIF - scan BEFORE BIF)
                if (includeHakFiles)
                {
                    // Scan dialog directory for module-specific HAKs (highest HAK priority)
                    if (!string.IsNullOrEmpty(_dialogFilePath))
                    {
                        var dialogDir = Path.GetDirectoryName(_dialogFilePath);
                        if (!string.IsNullOrEmpty(dialogDir) && Directory.Exists(dialogDir))
                        {
                            await ScanPathForHaksAsync(dialogDir);
                        }
                    }

                    // Scan NWN user hak folder
                    var userPath = SettingsService.Instance.NeverwinterNightsPath;
                    if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
                    {
                        var hakFolder = Path.Combine(userPath, "hak");
                        if (Directory.Exists(hakFolder))
                        {
                            await ScanPathForHaksAsync(hakFolder);
                        }
                    }

                    // Scan HAK files in game data folders
                    var basePath = SettingsService.Instance.BaseGameInstallPath;
                    if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
                    {
                        var dataPath = Path.Combine(basePath, "data");
                        if (Directory.Exists(dataPath))
                        {
                            await ScanPathForHaksAsync(dataPath);
                        }

                        var langPath = Path.Combine(basePath, "lang");
                        if (Directory.Exists(langPath))
                        {
                            foreach (var langDir in Directory.GetDirectories(langPath))
                            {
                                var langDataPath = Path.Combine(langDir, "data");
                                if (Directory.Exists(langDataPath))
                                {
                                    await ScanPathForHaksAsync(langDataPath);
                                }
                            }
                        }
                    }
                }

                // 3. BIF archives (lowest priority - base game resources)
                if (includeBifFiles)
                {
                    var basePath = SettingsService.Instance.BaseGameInstallPath;
                    if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
                    {
                        await ScanBifArchivesAsync(basePath);
                    }
                }

                // 4. Other location (user-specified folder - separate from NWN priority)
                if (includeOtherLocation && !string.IsNullOrEmpty(_overridePath))
                {
                    ScanPathForSounds(_overridePath, "Other");
                    await ScanPathForHaksAsync(_overridePath);
                }

                if (_allSounds.Count == 0)
                {
                    var msg = "No sounds found";
                    if (!includeGameResources && string.IsNullOrEmpty(SettingsService.Instance.BaseGameInstallPath))
                        msg = "Configure game path in Settings, or use Other location";
                    else if (includeOtherLocation && string.IsNullOrEmpty(_overridePath))
                        msg = "Click browse... to select a folder";

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

        private async Task ScanPathForHaksAsync(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return;

                var hakFiles = Directory.GetFiles(path, "*.hak", SearchOption.TopDirectoryOnly);

                for (int i = 0; i < hakFiles.Length; i++)
                {
                    var hakFile = hakFiles[i];
                    var hakName = Path.GetFileName(hakFile);

                    // Update progress on UI thread
                    FileCountLabel.Text = $"Loading HAK {i + 1}/{hakFiles.Length}: {hakName}...";

                    // Scan HAK on background thread to avoid blocking UI
                    await Task.Run(() => ScanHakForSounds(hakFile));
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
                var hakFileName = Path.GetFileName(hakPath);
                var lastModified = File.GetLastWriteTimeUtc(hakPath);

                // Check cache first
                if (_hakCache.TryGetValue(hakPath, out var cached) && cached.LastModified == lastModified)
                {
                    // Use cached sounds - deep copy to avoid shared state issues
                    foreach (var sound in cached.Sounds)
                    {
                        if (!_allSounds.Any(s => s.FileName.Equals(sound.FileName, StringComparison.OrdinalIgnoreCase)))
                        {
                            _allSounds.Add(new SoundFileInfo
                            {
                                FileName = sound.FileName,
                                FullPath = sound.FullPath,
                                IsMono = sound.IsMono,
                                Source = sound.Source,
                                HakPath = sound.HakPath,
                                ErfEntry = sound.ErfEntry
                            });
                        }
                    }
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Sound Browser: Used cached {cached.Sounds.Count} sounds from {hakFileName}");
                    return;
                }

                // Not cached or outdated - scan HAK
                var erf = ErfReader.Read(hakPath);
                var wavResources = erf.GetResourcesByType(ResourceTypes.Wav).ToList();
                var newCacheEntry = new HakCacheEntry
                {
                    HakPath = hakPath,
                    LastModified = lastModified,
                    Sounds = new List<SoundFileInfo>()
                };

                foreach (var resource in wavResources)
                {
                    var fileName = $"{resource.ResRef}.wav";
                    var soundInfo = new SoundFileInfo
                    {
                        FileName = fileName,
                        FullPath = hakPath,
                        IsMono = true, // Assume mono until verified
                        Source = hakFileName,
                        HakPath = hakPath,
                        ErfEntry = resource
                    };

                    // Add to cache
                    newCacheEntry.Sounds.Add(soundInfo);

                    // Add to current list if not duplicate
                    if (!_allSounds.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _allSounds.Add(soundInfo);
                    }
                }

                // Update cache
                _hakCache[hakPath] = newCacheEntry;

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Sound Browser: Scanned and cached {wavResources.Count} WAV resources in {hakFileName}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {hakPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Scan BIF archives for WAV resources using KEY file index.
        /// Issue #220: BIF files not being scanned
        /// </summary>
        private async Task ScanBifArchivesAsync(string basePath)
        {
            try
            {
                // Find KEY file - NWN:EE uses nwn_base.key in data/ folder
                var keyPaths = new[]
                {
                    Path.Combine(basePath, "data", "nwn_base.key"),
                    Path.Combine(basePath, "nwn_base.key"),
                    Path.Combine(basePath, "chitin.key") // Classic NWN
                };

                string? keyPath = null;
                foreach (var path in keyPaths)
                {
                    if (File.Exists(path))
                    {
                        keyPath = path;
                        break;
                    }
                }

                if (keyPath == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Sound Browser: No KEY file found for BIF scanning");
                    return;
                }

                FileCountLabel.Text = "Loading base game sounds from BIF archives...";

                // Load or get cached KEY file
                var keyFile = await Task.Run(() => GetOrLoadKeyFile(keyPath));
                if (keyFile == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound Browser: Could not load KEY file: {UnifiedLogger.SanitizePath(keyPath)}");
                    return;
                }

                // Get all WAV resources from KEY
                var wavResources = keyFile.GetResourcesByType(ResourceTypes.Wav).ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Sound Browser: Found {wavResources.Count} WAV resources in KEY file");

                // Group resources by BIF file for efficient loading
                var resourcesByBif = wavResources.GroupBy(r => r.BifIndex).ToList();
                var processedCount = 0;

                foreach (var bifGroup in resourcesByBif)
                {
                    var bifIndex = bifGroup.Key;
                    if (bifIndex >= keyFile.BifEntries.Count)
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound Browser: Invalid BIF index {bifIndex}");
                        continue;
                    }

                    var bifEntry = keyFile.BifEntries[bifIndex];
                    var bifPath = ResolveBifPath(basePath, bifEntry.Filename);

                    if (bifPath == null || !File.Exists(bifPath))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Sound Browser: BIF not found: {bifEntry.Filename}");
                        continue;
                    }

                    var bifName = Path.GetFileName(bifPath);
                    FileCountLabel.Text = $"Loading sounds from {bifName}...";

                    // Load BIF file (cached)
                    var bifFile = await Task.Run(() => GetOrLoadBifFile(bifPath));
                    if (bifFile == null)
                        continue;

                    foreach (var resource in bifGroup)
                    {
                        var fileName = $"{resource.ResRef}.wav";

                        // Skip if already found from higher priority source
                        if (_allSounds.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var soundInfo = new SoundFileInfo
                        {
                            FileName = fileName,
                            FullPath = bifPath,
                            IsMono = true, // Assume mono until verified
                            Source = $"BIF:{bifName}",
                            BifInfo = new BifSoundInfo
                            {
                                ResRef = resource.ResRef,
                                BifPath = bifPath,
                                VariableTableIndex = resource.VariableTableIndex,
                                FileSize = 0 // Will be determined on extraction
                            }
                        };

                        _allSounds.Add(soundInfo);
                        processedCount++;
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Sound Browser: Added {processedCount} WAV resources from BIF archives");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning BIF archives: {ex.Message}");
            }
        }

        /// <summary>
        /// Get or load a cached KEY file.
        /// </summary>
        private KeyFile? GetOrLoadKeyFile(string keyPath)
        {
            try
            {
                var lastModified = File.GetLastWriteTimeUtc(keyPath);

                // Check cache
                if (_keyCache.TryGetValue(keyPath, out var cached) && cached.LastModified == lastModified)
                {
                    return cached.KeyFile;
                }

                // Load KEY file
                var keyFile = KeyReader.Read(keyPath);
                _keyCache[keyPath] = new KeyCacheEntry
                {
                    KeyPath = keyPath,
                    LastModified = lastModified,
                    KeyFile = keyFile
                };

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Sound Browser: Loaded KEY file with {keyFile.ResourceEntries.Count} resources from {keyFile.BifEntries.Count} BIFs");

                return keyFile;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading KEY file {keyPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get or load a cached BIF file.
        /// </summary>
        private BifFile? GetOrLoadBifFile(string bifPath)
        {
            try
            {
                // Check cache
                if (_bifCache.TryGetValue(bifPath, out var cached))
                {
                    return cached;
                }

                // Load BIF file
                var bifFile = BifReader.Read(bifPath, keepBuffer: true);
                _bifCache[bifPath] = bifFile;

                return bifFile;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading BIF file {bifPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolve a BIF filename from KEY to an actual file path.
        /// </summary>
        private string? ResolveBifPath(string basePath, string bifFilename)
        {
            // Normalize path separators
            var normalized = bifFilename.Replace("\\", "/").Replace("/", Path.DirectorySeparatorChar.ToString());

            // Try relative to base path (for "data\file.bif" paths)
            var fullPath = Path.Combine(basePath, normalized);
            if (File.Exists(fullPath))
                return fullPath;

            // Try just the filename in data folder
            var dataPath = Path.Combine(basePath, "data", Path.GetFileName(normalized));
            if (File.Exists(dataPath))
                return dataPath;

            // Try in lang folders (for language-specific BIFs)
            var langPath = Path.Combine(basePath, "lang");
            if (Directory.Exists(langPath))
            {
                foreach (var langDir in Directory.GetDirectories(langPath))
                {
                    var langBifPath = Path.Combine(langDir, normalized);
                    if (File.Exists(langBifPath))
                        return langBifPath;

                    langBifPath = Path.Combine(langDir, "data", Path.GetFileName(normalized));
                    if (File.Exists(langBifPath))
                        return langBifPath;
                }
            }

            return null;
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

                // Priority: invalid WAV > stereo > source type
                if (!sound.IsValidWav)
                {
                    // Invalid WAV file (already validated and found to be non-standard)
                    displayName = $"❌ {baseName}{sourceInfo} [invalid]";
                    foreground = ErrorBrush;
                }
                else if (!sound.IsMono)
                {
                    displayName = $"⚠️ {baseName}{sourceInfo} [stereo]";
                    foreground = WarningBrush;
                }
                else if (sound.IsFromHak)
                {
                    displayName = $"📦 {baseName}{sourceInfo}";
                    foreground = HakBrush;
                }
                else if (sound.IsFromBif)
                {
                    displayName = $"🎮 {baseName}{sourceInfo}";
                    foreground = ForegroundBrush; // BIF sounds are base game, use normal color
                }
                else
                {
                    displayName = $"{baseName}{sourceInfo}";
                    foreground = ForegroundBrush;
                }

                SoundListBox.Items.Add(new ListBoxItem
                {
                    Content = displayName,
                    Tag = sound,
                    Foreground = foreground
                });
            }

            // Build count text with HAK/BIF/invalid counts
            var hakCount = _filteredSounds.Count(s => s.IsFromHak);
            var bifCount = _filteredSounds.Count(s => s.IsFromBif);
            var stereoCount = _filteredSounds.Count(s => !s.IsMono);
            var invalidCount = _filteredSounds.Count(s => !s.IsValidWav);
            var countText = $"{soundsToDisplay.Count} sound{(soundsToDisplay.Count == 1 ? "" : "s")}";

            var details = new List<string>();
            if (bifCount > 0)
                details.Add($"{bifCount} from BIF");
            if (hakCount > 0)
                details.Add($"{hakCount} from HAK");
            if (!monoOnly && stereoCount > 0)
                details.Add($"{stereoCount} stereo");
            if (invalidCount > 0)
                details.Add($"{invalidCount} invalid");

            if (details.Count > 0)
                countText += $" ({string.Join(", ", details)})";

            FileCountLabel.Text = countText;
            FileCountLabel.Foreground = ForegroundBrush;
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
                _selectedSoundPath = (soundInfo.IsFromHak || soundInfo.IsFromBif) ? null : soundInfo.FullPath;
                SelectedSoundLabel.Text = soundInfo.IsFromHak
                    ? $"{soundInfo.FileName} 📦"
                    : soundInfo.IsFromBif
                        ? $"{soundInfo.FileName} 🎮"
                        : soundInfo.FileName;
                PlayButton.IsEnabled = true;

                // Reset status label before validation (clear stale messages from previous selection)
                FileCountLabel.Text = "Validating...";
                FileCountLabel.Foreground = SecondaryBrush;

                // Validate sound file format against NWN specs
                var skipValidation = DisableValidationCheckBox?.IsChecked == true;
                try
                {
                    if (soundInfo.IsFromHak)
                    {
                        if (skipValidation)
                        {
                            // Skip validation - just show source info
                            FileCountLabel.Text = $"📦 From: {soundInfo.Source}";
                            FileCountLabel.Foreground = HakBrush;
                        }
                        else
                        {
                            // For HAK sounds, extract temporarily to validate format
                            ValidateHakSound(soundInfo);
                        }
                    }
                    else if (soundInfo.IsFromBif)
                    {
                        if (skipValidation)
                        {
                            // Skip validation - just show source info
                            FileCountLabel.Text = $"🎮 From: {soundInfo.Source}";
                            FileCountLabel.Foreground = ForegroundBrush;
                        }
                        else
                        {
                            // For BIF sounds, extract temporarily to validate format
                            ValidateBifSound(soundInfo);
                        }
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
                                ? WarningBrush
                                : ErrorBrush;
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
                        CurrentSoundLabel.Foreground = ErrorBrush;
                        return;
                    }
                    pathToPlay = extractedPath;
                }
                else if (_selectedSoundInfo.IsFromBif)
                {
                    // Extract from BIF to temp file for playback
                    var extractedPath = ExtractBifSoundToTemp(_selectedSoundInfo);
                    if (extractedPath == null)
                    {
                        CurrentSoundLabel.Text = $"❌ Failed to extract from BIF";
                        CurrentSoundLabel.Foreground = ErrorBrush;
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
                        CurrentSoundLabel.Foreground = WarningBrush;
                        return;
                    }
                }

                // Warn about invalid WAV but still attempt playback
                var invalidWarning = !_selectedSoundInfo.IsValidWav
                    ? " ⚠️ (invalid format)"
                    : "";

                _audioService.Play(pathToPlay);
                CurrentSoundLabel.Text = _selectedSoundInfo.IsFromHak
                    ? $"Playing: {_selectedSound} (from HAK){invalidWarning}"
                    : _selectedSoundInfo.IsFromBif
                        ? $"Playing: {_selectedSound} (from BIF){invalidWarning}"
                        : $"Playing: {_selectedSound}{invalidWarning}";
                CurrentSoundLabel.Foreground = !_selectedSoundInfo.IsValidWav ? WarningBrush : SuccessBrush;
                StopButton.IsEnabled = true;
                PlayButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to play sound: {ex.Message}");
                // Provide more context if this was an invalid WAV
                var hint = !_selectedSoundInfo.IsValidWav
                    ? $" ({_selectedSoundInfo.InvalidWavReason})"
                    : "";
                CurrentSoundLabel.Text = $"❌ Error: {ex.Message}{hint}";
                CurrentSoundLabel.Foreground = ErrorBrush;
            }
        }

        private void ValidateHakSound(SoundFileInfo soundInfo)
        {
            if (soundInfo.HakPath == null || soundInfo.ErfEntry == null)
            {
                FileCountLabel.Text = $"📦 From: {soundInfo.Source}";
                FileCountLabel.Foreground = HakBrush;
                return;
            }

            try
            {
                // Extract sound data from HAK (in memory) to validate
                var soundData = ErfReader.ExtractResource(soundInfo.HakPath, soundInfo.ErfEntry);

                // Write to temp file for validation - sanitize ResRef for filename safety
                var safeResRef = SanitizeForFileName(soundInfo.ErfEntry.ResRef);
                var tempPath = Path.Combine(Path.GetTempPath(), $"pv_{safeResRef}.wav");
                File.WriteAllBytes(tempPath, soundData);

                try
                {
                    // Skip filename check - we're validating temp file, not the actual ResRef
                    // HAK ResRefs are already valid (they came from a working HAK file)
                    var validation = SoundValidator.Validate(tempPath, isVoiceOrSfx: true, skipFilenameCheck: true);

                    // Update the cached mono status and validity
                    soundInfo.IsMono = validation.IsMono;
                    soundInfo.IsValidWav = validation.IsValidWav;
                    soundInfo.InvalidWavReason = validation.InvalidWavReason;

                    var sourceInfo = $" (📦 {soundInfo.Source})";

                    // Show invalid WAV warning but don't block
                    if (!validation.IsValidWav)
                    {
                        FileCountLabel.Text = $"⚠️ {validation.InvalidWavReason}{sourceInfo} - playback may fail";
                        FileCountLabel.Foreground = WarningBrush;
                    }
                    else if (validation.HasIssues)
                    {
                        var issues = string.Join(", ", validation.Errors.Concat(validation.Warnings));
                        FileCountLabel.Text = validation.IsValid
                            ? $"⚠ {issues}{sourceInfo}"
                            : $"❌ {issues}{sourceInfo}";
                        FileCountLabel.Foreground = validation.IsValid
                            ? WarningBrush
                            : ErrorBrush;
                    }
                    else if (!string.IsNullOrEmpty(validation.FormatInfo))
                    {
                        FileCountLabel.Text = $"✓ {validation.FormatInfo}{sourceInfo}";
                        FileCountLabel.Foreground = SuccessBrush;
                    }
                    else
                    {
                        FileCountLabel.Text = $"📦 From: {soundInfo.Source}";
                        FileCountLabel.Foreground = HakBrush;
                    }
                }
                finally
                {
                    // Clean up temp validation file
                    try { File.Delete(tempPath); }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.TRACE, $"Could not delete temp file {UnifiedLogger.SanitizePath(tempPath)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not validate HAK sound: {ex.Message}");
                FileCountLabel.Text = $"📦 From: {soundInfo.Source} (validation unavailable)";
                FileCountLabel.Foreground = HakBrush;
            }
        }

        private string? ExtractHakSoundToTemp(SoundFileInfo soundInfo)
        {
            if (soundInfo.HakPath == null || soundInfo.ErfEntry == null)
                return null;

            try
            {
                // Extract to temp directory - sanitize ResRef for filename safety
                var tempDir = Path.GetTempPath();
                var safeResRef = SanitizeForFileName(soundInfo.ErfEntry.ResRef);
                var tempFileName = $"ps_{safeResRef}.wav";
                var tempPath = Path.Combine(tempDir, tempFileName);

                // If same file already exists, reuse it (allows replaying same sound)
                if (_tempExtractedPath == tempPath && File.Exists(tempPath))
                {
                    return tempPath;
                }

                // Clean up previous temp file only if different
                CleanupTempFile();

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

        /// <summary>
        /// Validate a BIF sound by extracting it temporarily.
        /// </summary>
        private void ValidateBifSound(SoundFileInfo soundInfo)
        {
            if (soundInfo.BifInfo == null)
            {
                FileCountLabel.Text = $"🎮 From: {soundInfo.Source}";
                FileCountLabel.Foreground = ForegroundBrush;
                return;
            }

            try
            {
                // Get cached BIF file
                if (!_bifCache.TryGetValue(soundInfo.BifInfo.BifPath, out var bifFile))
                {
                    FileCountLabel.Text = $"🎮 From: {soundInfo.Source}";
                    FileCountLabel.Foreground = ForegroundBrush;
                    return;
                }

                // Extract sound data
                var soundData = bifFile.ExtractVariableResource(soundInfo.BifInfo.VariableTableIndex);
                if (soundData == null)
                {
                    FileCountLabel.Text = $"🎮 From: {soundInfo.Source} (extraction failed)";
                    FileCountLabel.Foreground = WarningBrush;
                    return;
                }

                // Write to temp file for validation
                var safeResRef = SanitizeForFileName(soundInfo.BifInfo.ResRef);
                var tempPath = Path.Combine(Path.GetTempPath(), $"pv_bif_{safeResRef}.wav");
                File.WriteAllBytes(tempPath, soundData);

                try
                {
                    var validation = SoundValidator.Validate(tempPath, isVoiceOrSfx: true, skipFilenameCheck: true);

                    // Update the cached mono status and validity
                    soundInfo.IsMono = validation.IsMono;
                    soundInfo.IsValidWav = validation.IsValidWav;
                    soundInfo.InvalidWavReason = validation.InvalidWavReason;

                    var sourceInfo = $" (🎮 {soundInfo.Source})";

                    // Show invalid WAV warning but don't block
                    if (!validation.IsValidWav)
                    {
                        FileCountLabel.Text = $"⚠️ {validation.InvalidWavReason}{sourceInfo} - playback may fail";
                        FileCountLabel.Foreground = WarningBrush;
                    }
                    else if (validation.HasIssues)
                    {
                        var issues = string.Join(", ", validation.Errors.Concat(validation.Warnings));
                        FileCountLabel.Text = validation.IsValid
                            ? $"⚠ {issues}{sourceInfo}"
                            : $"❌ {issues}{sourceInfo}";
                        FileCountLabel.Foreground = validation.IsValid
                            ? WarningBrush
                            : ErrorBrush;
                    }
                    else if (!string.IsNullOrEmpty(validation.FormatInfo))
                    {
                        FileCountLabel.Text = $"✓ {validation.FormatInfo}{sourceInfo}";
                        FileCountLabel.Foreground = SuccessBrush;
                    }
                    else
                    {
                        FileCountLabel.Text = $"🎮 From: {soundInfo.Source}";
                        FileCountLabel.Foreground = ForegroundBrush;
                    }
                }
                finally
                {
                    // Clean up temp validation file
                    try { File.Delete(tempPath); }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.TRACE, $"Could not delete temp file {UnifiedLogger.SanitizePath(tempPath)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not validate BIF sound: {ex.Message}");
                FileCountLabel.Text = $"🎮 From: {soundInfo.Source} (validation unavailable)";
                FileCountLabel.Foreground = ForegroundBrush;
            }
        }

        /// <summary>
        /// Extract a BIF sound to a temp file for playback.
        /// </summary>
        private string? ExtractBifSoundToTemp(SoundFileInfo soundInfo)
        {
            if (soundInfo.BifInfo == null)
                return null;

            try
            {
                // Extract to temp directory
                var tempDir = Path.GetTempPath();
                var safeResRef = SanitizeForFileName(soundInfo.BifInfo.ResRef);
                var tempFileName = $"ps_bif_{safeResRef}.wav";
                var tempPath = Path.Combine(tempDir, tempFileName);

                // If same file already exists, reuse it (allows replaying same sound)
                if (_tempExtractedPath == tempPath && File.Exists(tempPath))
                {
                    return tempPath;
                }

                // Clean up previous temp file only if different
                CleanupTempFile();

                // Get cached BIF file
                if (!_bifCache.TryGetValue(soundInfo.BifInfo.BifPath, out var bifFile))
                {
                    bifFile = GetOrLoadBifFile(soundInfo.BifInfo.BifPath);
                    if (bifFile == null)
                        return null;
                }

                var soundData = bifFile.ExtractVariableResource(soundInfo.BifInfo.VariableTableIndex);
                if (soundData == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to extract BIF sound: resource index {soundInfo.BifInfo.VariableTableIndex}");
                    return null;
                }

                File.WriteAllBytes(tempPath, soundData);
                _tempExtractedPath = tempPath;

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Extracted BIF sound to temp: {tempFileName}");
                return tempPath;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to extract BIF sound: {ex.Message}");
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

        /// <summary>
        /// Sanitize a ResRef for use as a filename - removes invalid chars and truncates to safe length.
        /// </summary>
        private static string SanitizeForFileName(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
                return "unknown";

            // Remove any characters that are invalid in filenames
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(resRef.Where(c => !invalidChars.Contains(c) && c != '\0').ToArray());

            // Truncate to 16 chars (NWN ResRef max) to prevent path too long errors
            if (sanitized.Length > 16)
                sanitized = sanitized.Substring(0, 16);

            return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
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
