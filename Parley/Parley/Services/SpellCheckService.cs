using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Radoub.Dictionary;

namespace DialogEditor.Services
{
    /// <summary>
    /// Provides spell-checking functionality for Parley dialogs.
    /// Wraps Radoub.Dictionary with theme-aware styling for error indicators.
    /// </summary>
    public class SpellCheckService : IDisposable
    {
        private static SpellCheckService? _instance;
        public static SpellCheckService Instance => _instance ??= new SpellCheckService();

        private readonly DictionaryManager _dictionaryManager;
        private SpellChecker? _spellChecker;
        private bool _disposed;
        private bool _isInitialized;

        /// <summary>
        /// Whether spell-checking is available and loaded.
        /// </summary>
        public bool IsReady => _isInitialized && _spellChecker != null;

        /// <summary>
        /// Event raised when spell-check is ready for use.
        /// </summary>
        public event EventHandler? Ready;

        private SpellCheckService()
        {
            _dictionaryManager = new DictionaryManager();
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
                _isInitialized = true;

                UnifiedLogger.LogApplication(LogLevel.INFO, "Spell-check initialized with en_US dictionary");
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
        /// </summary>
        public void AddToCustomDictionary(string word)
        {
            if (!string.IsNullOrWhiteSpace(word))
            {
                _dictionaryManager.AddWord(word.Trim());
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added '{word}' to custom dictionary");
            }
        }

        /// <summary>
        /// Get the number of session-ignored words.
        /// </summary>
        public int SessionIgnoredCount => _spellChecker?.SessionIgnoredCount ?? 0;

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
        /// Load custom dictionary from JSON file.
        /// </summary>
        public async Task LoadCustomDictionaryAsync(string path)
        {
            try
            {
                await _dictionaryManager.LoadDictionaryAsync(path);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded custom dictionary from {path}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load custom dictionary: {ex.Message}");
            }
        }

        /// <summary>
        /// Save custom dictionary to file.
        /// </summary>
        public async Task SaveCustomDictionaryAsync(string path, string source = "Parley Custom Dictionary")
        {
            try
            {
                await _dictionaryManager.ExportDictionaryAsync(path, source, "User-added custom words");
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Saved custom dictionary to {path}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to save custom dictionary: {ex.Message}");
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
