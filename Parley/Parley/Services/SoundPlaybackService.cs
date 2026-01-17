using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models.Sound;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for finding and playing sounds from all NWN sources (loose files, HAK, BIF).
    /// Used by MainWindow to play sounds from the property panel.
    /// Issue #895: Sound play button works in Sound Browser but not Main Window.
    /// </summary>
    public class SoundPlaybackService : IDisposable
    {
        private readonly AudioService _audioService;
        private readonly SoundScanner _scanner;
        private readonly SoundExtractor _extractor;
        private readonly SettingsService _settings;

        // Cache of scanned sounds (populated on first use or when settings change)
        private List<SoundFileInfo>? _soundCache;
        private string? _lastScannedPath;

        public SoundPlaybackService(AudioService audioService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _settings = SettingsService.Instance;
            _scanner = new SoundScanner(_settings);
            _extractor = new SoundExtractor();
        }

        /// <summary>
        /// Finds and plays a sound by filename (with or without extension).
        /// Searches loose files first, then HAK archives, then BIF archives.
        /// </summary>
        /// <returns>True if sound was found and playback started, false otherwise.</returns>
        public async Task<SoundPlayResult> PlaySoundAsync(string soundName)
        {
            if (string.IsNullOrWhiteSpace(soundName))
            {
                return new SoundPlayResult { Success = false, ErrorMessage = "No sound file specified" };
            }

            try
            {
                // Normalize the filename (add .wav if no extension)
                var normalizedName = NormalizeSoundName(soundName);

                // Try to find the sound
                var soundInfo = await FindSoundAsync(normalizedName);

                if (soundInfo == null)
                {
                    return new SoundPlayResult
                    {
                        Success = false,
                        ErrorMessage = $"Sound file not found: {soundName}"
                    };
                }

                // Get the path to play (may need extraction from archive)
                string? pathToPlay = GetPlayablePath(soundInfo);

                if (pathToPlay == null)
                {
                    return new SoundPlayResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to extract sound: {soundName}"
                    };
                }

                // Play the sound
                _audioService.Play(pathToPlay);

                var sourceLabel = soundInfo.IsFromHak ? " (from HAK)"
                    : soundInfo.IsFromBif ? " (from BIF)"
                    : "";

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Playing sound: {soundName}{sourceLabel}");

                return new SoundPlayResult
                {
                    Success = true,
                    SoundInfo = soundInfo,
                    SourceLabel = sourceLabel
                };
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error playing sound '{soundName}': {ex.Message}");
                return new SoundPlayResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Stops current playback.
        /// </summary>
        public void Stop()
        {
            _audioService.Stop();
        }

        /// <summary>
        /// Gets whether audio is currently playing.
        /// </summary>
        public bool IsPlaying => _audioService.IsPlaying;

        /// <summary>
        /// Event raised when playback stops.
        /// </summary>
        public event EventHandler? PlaybackStopped
        {
            add => _audioService.PlaybackStopped += value;
            remove => _audioService.PlaybackStopped -= value;
        }

        private string NormalizeSoundName(string soundName)
        {
            var name = soundName.Trim();

            // Add .wav extension if not present
            if (!name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".bmu", StringComparison.OrdinalIgnoreCase))
            {
                name += ".wav";
            }

            return name;
        }

        private async Task<SoundFileInfo?> FindSoundAsync(string fileName)
        {
            // Check if we need to rebuild the cache
            var currentPath = _settings.NeverwinterNightsPath ?? _settings.BaseGameInstallPath ?? "";
            if (_soundCache == null || _lastScannedPath != currentPath)
            {
                await RebuildCacheAsync();
                _lastScannedPath = currentPath;
            }

            if (_soundCache == null)
                return null;

            // Search the cache (case-insensitive)
            return _soundCache.FirstOrDefault(s =>
                s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }

        private async Task RebuildCacheAsync()
        {
            _soundCache = new List<SoundFileInfo>();

            // Scan loose files from user Documents path
            var userPath = _settings.NeverwinterNightsPath;
            if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
            {
                _soundCache.AddRange(_scanner.ScanAllSoundFolders(userPath, _soundCache));
            }

            // Scan loose files from game installation
            var installPath = _settings.BaseGameInstallPath;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                _soundCache.AddRange(_scanner.ScanAllSoundFolders(installPath, _soundCache));

                var dataPath = Path.Combine(installPath, "data");
                if (Directory.Exists(dataPath))
                {
                    _soundCache.AddRange(_scanner.ScanAllSoundFolders(dataPath, _soundCache));
                }
            }

            // Scan HAK files if enabled
            if (_settings.SoundBrowserIncludeHakFiles)
            {
                var hakPath = !string.IsNullOrEmpty(userPath) ? Path.Combine(userPath, "hak") : null;
                if (!string.IsNullOrEmpty(hakPath) && Directory.Exists(hakPath))
                {
                    var hakSounds = await _scanner.ScanPathForHaksAsync(hakPath, _soundCache);
                    _soundCache.AddRange(hakSounds);
                }
            }

            // Scan BIF files if enabled
            if (_settings.SoundBrowserIncludeBifFiles && !string.IsNullOrEmpty(installPath))
            {
                var bifSounds = await _scanner.ScanBifArchivesAsync(installPath, _soundCache);
                _soundCache.AddRange(bifSounds);
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SoundPlaybackService: Cached {_soundCache.Count} sounds");
        }

        private string? GetPlayablePath(SoundFileInfo soundInfo)
        {
            if (soundInfo.IsFromHak)
            {
                return _extractor.ExtractHakSoundToTemp(soundInfo);
            }

            if (soundInfo.IsFromBif)
            {
                return _extractor.ExtractBifSoundToTemp(soundInfo);
            }

            // Loose file - verify it exists
            if (File.Exists(soundInfo.FullPath))
            {
                return soundInfo.FullPath;
            }

            UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound file not found on disk: {soundInfo.FullPath}");
            return null;
        }

        /// <summary>
        /// Clears the sound cache (forces rescan on next play).
        /// </summary>
        public void ClearCache()
        {
            _soundCache = null;
            _lastScannedPath = null;
        }

        public void Dispose()
        {
            _extractor.CleanupTempFile();
        }
    }

    /// <summary>
    /// Result of attempting to play a sound.
    /// </summary>
    public class SoundPlayResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public SoundFileInfo? SoundInfo { get; set; }
        public string SourceLabel { get; set; } = "";
    }
}
