using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages editor behavior preferences for Parley (auto-save, NPC coloring,
    /// confirmations, simulator, script editor, tool integration, sound browser,
    /// spell check, parameter cache configuration).
    /// Extracted from SettingsService for single responsibility (#1269).
    /// </summary>
    public class EditorPreferencesService : INotifyPropertyChanged
    {
        private readonly ParameterCacheService _parameterCache;

        // Auto-save settings
        private bool _autoSaveEnabled = true;
        private int _autoSaveDelayMs = 2000;
        private int _autoSaveIntervalMinutes = 0;

        // NPC coloring
        private bool _enableNpcTagColoring = true;

        // Confirmation dialogs
        private bool _showDeleteConfirmation = true;

        // Conversation Simulator
        private bool _simulatorShowWarnings = true;

        // Script editor settings
        private string _externalEditorPath = "";
        private List<string> _scriptSearchPaths = new List<string>();

        // Radoub tool integration
        private string _manifestPath = "";

        // Parameter cache settings
        private bool _enableParameterCache = true;
        private int _maxCachedValuesPerParameter = 10;
        private int _maxCachedScripts = 1000;

        // Sound Browser settings
        private bool _soundBrowserIncludeGameResources = false;
        private bool _soundBrowserIncludeHakFiles = false;
        private bool _soundBrowserIncludeBifFiles = false;

        // Spell Check settings
        private bool _spellCheckEnabled = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Called when settings need to be saved. SettingsService subscribes to this.
        /// </summary>
        public event Action? SettingsChanged;

        public EditorPreferencesService(ParameterCacheService parameterCache)
        {
            _parameterCache = parameterCache;
        }

        /// <summary>
        /// Initializes the service with loaded settings data.
        /// Called by SettingsService during LoadSettings().
        /// </summary>
        public void Initialize(
            bool autoSaveEnabled, int autoSaveDelayMs, int autoSaveIntervalMinutes,
            bool enableNpcTagColoring, bool showDeleteConfirmation, bool simulatorShowWarnings,
            string externalEditorPath, List<string> scriptSearchPaths,
            string manifestPath,
            bool enableParameterCache, int maxCachedValuesPerParameter, int maxCachedScripts,
            bool soundBrowserIncludeGameResources, bool soundBrowserIncludeHakFiles, bool soundBrowserIncludeBifFiles,
            bool spellCheckEnabled)
        {
            _autoSaveEnabled = autoSaveEnabled;
            _autoSaveDelayMs = Math.Max(1000, Math.Min(10000, autoSaveDelayMs));
            _autoSaveIntervalMinutes = Math.Max(0, Math.Min(60, autoSaveIntervalMinutes));

            _enableNpcTagColoring = enableNpcTagColoring;
            _showDeleteConfirmation = showDeleteConfirmation;
            _simulatorShowWarnings = simulatorShowWarnings;

            _externalEditorPath = externalEditorPath ?? "";
            _scriptSearchPaths = scriptSearchPaths ?? new List<string>();

            _manifestPath = manifestPath ?? "";

            _enableParameterCache = enableParameterCache;
            _maxCachedValuesPerParameter = Math.Max(5, Math.Min(50, maxCachedValuesPerParameter));
            _maxCachedScripts = Math.Max(100, Math.Min(10000, maxCachedScripts));

            // Apply parameter cache settings
            _parameterCache.EnableCaching = _enableParameterCache;
            _parameterCache.MaxValuesPerParameter = _maxCachedValuesPerParameter;
            _parameterCache.MaxScriptsInCache = _maxCachedScripts;

            _soundBrowserIncludeGameResources = soundBrowserIncludeGameResources;
            _soundBrowserIncludeHakFiles = soundBrowserIncludeHakFiles;
            _soundBrowserIncludeBifFiles = soundBrowserIncludeBifFiles;

            _spellCheckEnabled = spellCheckEnabled;
        }

        // Auto-Save Settings
        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set
            {
                if (SetProperty(ref _autoSaveEnabled, value))
                {
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-save {(value ? "enabled" : "disabled")}");
                }
            }
        }

        public int AutoSaveDelayMs
        {
            get => _autoSaveDelayMs;
            set
            {
                if (SetProperty(ref _autoSaveDelayMs, Math.Max(1000, Math.Min(10000, value))))
                {
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-save delay set to {value}ms");
                }
            }
        }

        public int AutoSaveIntervalMinutes
        {
            get => _autoSaveIntervalMinutes;
            set
            {
                var clampedValue = Math.Max(0, Math.Min(60, value));
                if (SetProperty(ref _autoSaveIntervalMinutes, clampedValue))
                {
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-save interval set to {clampedValue} minutes");
                }
            }
        }

        public int EffectiveAutoSaveIntervalMs
        {
            get
            {
                if (_autoSaveIntervalMinutes > 0)
                    return _autoSaveIntervalMinutes * 60 * 1000;
                return _autoSaveDelayMs;
            }
        }

        // NPC Coloring
        public bool EnableNpcTagColoring
        {
            get => _enableNpcTagColoring;
            set
            {
                if (SetProperty(ref _enableNpcTagColoring, value))
                {
                    SettingsChanged?.Invoke();
                }
            }
        }

        // Confirmation Dialogs
        public bool ShowDeleteConfirmation
        {
            get => _showDeleteConfirmation;
            set
            {
                if (SetProperty(ref _showDeleteConfirmation, value))
                {
                    SettingsChanged?.Invoke();
                }
            }
        }

        // Conversation Simulator
        public bool SimulatorShowWarnings
        {
            get => _simulatorShowWarnings;
            set
            {
                if (SetProperty(ref _simulatorShowWarnings, value))
                {
                    SettingsChanged?.Invoke();
                }
            }
        }

        // Script Editor Settings
        public string ExternalEditorPath
        {
            get => _externalEditorPath;
            set { if (SetProperty(ref _externalEditorPath, value ?? "")) SettingsChanged?.Invoke(); }
        }

        public List<string> ScriptSearchPaths
        {
            get => _scriptSearchPaths;
            set
            {
                _scriptSearchPaths = value ?? new List<string>();
                OnPropertyChanged(nameof(ScriptSearchPaths));
                SettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Internal access to the backing list for serialization by SettingsService.
        /// </summary>
        internal List<string> ScriptSearchPathsInternal => _scriptSearchPaths;

        // Radoub Tool Integration
        public string ManifestPath
        {
            get => _manifestPath;
            set { if (SetProperty(ref _manifestPath, value ?? "")) SettingsChanged?.Invoke(); }
        }

        // Parameter Cache Settings
        public bool EnableParameterCache
        {
            get => _enableParameterCache;
            set
            {
                if (SetProperty(ref _enableParameterCache, value))
                {
                    _parameterCache.EnableCaching = value;
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Parameter cache {(value ? "enabled" : "disabled")}");
                }
            }
        }

        public int MaxCachedValuesPerParameter
        {
            get => _maxCachedValuesPerParameter;
            set
            {
                if (SetProperty(ref _maxCachedValuesPerParameter, Math.Max(5, Math.Min(50, value))))
                {
                    _parameterCache.MaxValuesPerParameter = value;
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Max cached values per parameter set to {value}");
                }
            }
        }

        public int MaxCachedScripts
        {
            get => _maxCachedScripts;
            set
            {
                if (SetProperty(ref _maxCachedScripts, Math.Max(100, Math.Min(10000, value))))
                {
                    _parameterCache.MaxScriptsInCache = value;
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Max cached scripts set to {value}");
                }
            }
        }

        // Sound Browser Settings
        public bool SoundBrowserIncludeGameResources
        {
            get => _soundBrowserIncludeGameResources;
            set { if (SetProperty(ref _soundBrowserIncludeGameResources, value)) SettingsChanged?.Invoke(); }
        }

        public bool SoundBrowserIncludeHakFiles
        {
            get => _soundBrowserIncludeHakFiles;
            set { if (SetProperty(ref _soundBrowserIncludeHakFiles, value)) SettingsChanged?.Invoke(); }
        }

        public bool SoundBrowserIncludeBifFiles
        {
            get => _soundBrowserIncludeBifFiles;
            set { if (SetProperty(ref _soundBrowserIncludeBifFiles, value)) SettingsChanged?.Invoke(); }
        }

        // Spell Check Settings
        public bool SpellCheckEnabled
        {
            get => _spellCheckEnabled;
            set
            {
                if (SetProperty(ref _spellCheckEnabled, value))
                {
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Spell check {(value ? "enabled" : "disabled")}");
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
