using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Radoub.Dictionary;

namespace Manifest.Services
{
    /// <summary>
    /// Provides spell-checking functionality for Manifest journal editor.
    /// Wraps Radoub.Dictionary with theme-aware styling for error indicators.
    /// </summary>
    /// <remarks>
    /// Custom dictionary is stored at ~/Radoub/Dictionaries/custom.dic (shared across all Radoub tools).
    /// This allows words added in Parley to appear in Manifest and vice versa.
    /// </remarks>
    public class SpellCheckService : IDisposable
    {
        private static SpellCheckService? _instance;
        public static SpellCheckService Instance => _instance ??= new SpellCheckService();

        private readonly DictionaryManager _dictionaryManager;
        private SpellChecker? _spellChecker;
        private bool _disposed;
        private bool _isInitialized;

        /// <summary>
        /// Path to the Radoub-wide custom dictionary file.
        /// Located at ~/Radoub/Dictionaries/custom.dic
        /// </summary>
        private readonly string _customDictionaryPath;

        /// <summary>
        /// Whether spell-checking is available and loaded.
        /// Also checks if spell-check is enabled in settings.
        /// </summary>
        public bool IsReady => _isInitialized && _spellChecker != null && SettingsService.Instance.SpellCheckEnabled;

        /// <summary>
        /// Event raised when spell-check is ready for use.
        /// </summary>
        public event EventHandler? Ready;

        private SpellCheckService()
        {
            _dictionaryManager = new DictionaryManager();

            // Setup custom dictionary path: ~/Radoub/Dictionaries/custom.dic (shared with Parley)
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dictionariesDir = Path.Combine(userProfile, "Radoub", "Dictionaries");
            Directory.CreateDirectory(dictionariesDir);
            _customDictionaryPath = Path.Combine(dictionariesDir, "custom.dic");
        }

        /// <summary>
        /// Initialize spell-checking with bundled English dictionary.
        /// Call this once at app startup.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                _spellChecker = new SpellChecker(_dictionaryManager);
                await _spellChecker.LoadBundledDictionaryAsync("en_US");

                // Load custom dictionary if it exists
                await LoadCustomDictionaryInternalAsync();

                _isInitialized = true;

                var totalWordCount = GetCustomWordCount();
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Spell-check initialized with en_US dictionary ({totalWordCount} custom words)");

                Ready?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to initialize spell-check: {ex.Message}");
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Check if a word is spelled correctly.
        /// </summary>
        public bool IsCorrect(string word)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(word))
                return true;

            return _spellChecker!.IsCorrect(word);
        }

        /// <summary>
        /// Get all spelling errors in text.
        /// </summary>
        public IEnumerable<SpellingError> CheckText(string text)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(text))
                return Enumerable.Empty<SpellingError>();

            return _spellChecker!.CheckText(text);
        }

        /// <summary>
        /// Get spelling suggestions for a misspelled word.
        /// </summary>
        public IEnumerable<string> GetSuggestions(string word, int maxSuggestions = 5)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(word))
                return Enumerable.Empty<string>();

            return _spellChecker!.GetSuggestions(word, maxSuggestions);
        }

        /// <summary>
        /// Ignore a word for the current session only.
        /// </summary>
        public void IgnoreForSession(string word)
        {
            _spellChecker?.IgnoreForSession(word);
        }

        /// <summary>
        /// Add a word to the custom dictionary permanently.
        /// Automatically saves to disk.
        /// </summary>
        public void AddToCustomDictionary(string word)
        {
            if (!string.IsNullOrWhiteSpace(word))
            {
                _dictionaryManager.AddWord(word.Trim());
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added '{word}' to custom dictionary");

                // Save immediately
                _ = SaveCustomDictionaryAsync();
            }
        }

        /// <summary>
        /// Get the number of session-ignored words.
        /// </summary>
        public int SessionIgnoredCount => _spellChecker?.SessionIgnoredCount ?? 0;

        /// <summary>
        /// Get the number of custom words.
        /// </summary>
        public int GetCustomWordCount() => _dictionaryManager.WordCount;

        /// <summary>
        /// Clear all session-ignored words.
        /// </summary>
        public void ClearSessionIgnored()
        {
            _spellChecker?.ClearSessionIgnored();
        }

        /// <summary>
        /// Creates a TextDecoration for spelling errors using theme-aware color.
        /// Uses dotted underline pattern for accessibility (WCAG 1.4.1 compliance).
        /// </summary>
        public TextDecoration CreateSpellingErrorDecoration()
        {
            // Get theme color or fall back to red
            var brush = GetSpellingErrorBrush();

            return new TextDecoration
            {
                Location = TextDecorationLocation.Underline,
                Stroke = brush,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double>(new[] { 1.0, 2.0 }),
                StrokeLineCap = PenLineCap.Round,
                StrokeThickness = 2
            };
        }

        /// <summary>
        /// Get the theme-aware brush for spelling errors.
        /// </summary>
        public IBrush GetSpellingErrorBrush()
        {
            // Try to get from theme resources
            if (Application.Current?.TryGetResource("ThemeSpellingError", Application.Current.ActualThemeVariant, out var resource) == true
                && resource is IBrush themeBrush)
            {
                return themeBrush;
            }

            // Fall back to error color
            if (Application.Current?.TryGetResource("ThemeError", Application.Current.ActualThemeVariant, out var errorResource) == true
                && errorResource is IBrush errorBrush)
            {
                return errorBrush;
            }

            // Ultimate fallback
            return new SolidColorBrush(Color.Parse("#D32F2F"));
        }

        /// <summary>
        /// Load the Radoub-wide custom dictionary if it exists.
        /// </summary>
        private async Task LoadCustomDictionaryInternalAsync()
        {
            if (!File.Exists(_customDictionaryPath))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "No custom dictionary file found, starting fresh");
                return;
            }

            try
            {
                await _dictionaryManager.LoadDictionaryAsync(_customDictionaryPath);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Loaded custom dictionary: {_dictionaryManager.WordCount} words");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load custom dictionary: {ex.Message}");
            }
        }

        /// <summary>
        /// Save the custom dictionary to disk.
        /// </summary>
        public async Task SaveCustomDictionaryAsync()
        {
            try
            {
                await _dictionaryManager.ExportDictionaryAsync(
                    _customDictionaryPath,
                    "Radoub Custom Dictionary",
                    "User-added custom words for spell checking");

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Saved custom dictionary: {_dictionaryManager.WordCount} words");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to save custom dictionary: {ex.Message}");
            }
        }

        /// <summary>
        /// Reload the custom dictionary from disk.
        /// Useful when another Radoub tool has updated the dictionary.
        /// </summary>
        public async Task ReloadCustomDictionaryAsync()
        {
            try
            {
                // Clear and reload
                await LoadCustomDictionaryInternalAsync();
                UnifiedLogger.LogApplication(LogLevel.INFO, "Reloaded custom dictionary");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to reload custom dictionary: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _spellChecker?.Dispose();
                _spellChecker = null;
                _disposed = true;
            }
        }
    }
}
