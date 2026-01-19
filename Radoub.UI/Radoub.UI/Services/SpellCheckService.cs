using System;
using Radoub.Formats.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Radoub.Dictionary;
using Radoub.Dictionary.Models;

namespace Radoub.UI.Services;

/// <summary>
/// Provides spell-checking functionality for all Radoub tools.
/// Wraps Radoub.Dictionary with theme-aware styling for error indicators.
/// Supports hot-swapping of dictionaries when settings change.
/// </summary>
/// <remarks>
/// Custom dictionary is stored at ~/Radoub/Dictionaries/custom.dic (shared across all Radoub tools).
/// This allows words added in any Radoub tool to be available in all other tools.
/// </remarks>
public class SpellCheckService : IDisposable
{
    private static SpellCheckService? _instance;
    public static SpellCheckService Instance => _instance ??= new SpellCheckService();

    private DictionaryManager _dictionaryManager;
    private SpellChecker? _spellChecker;
    private DictionaryDiscovery? _discovery;
    private bool _disposed;
    private bool _isInitialized;
    private bool _isReloading;

    /// <summary>
    /// Tracks words added by the user (separate from loaded dictionaries).
    /// </summary>
    private readonly HashSet<string> _userAddedWords = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Path to the Radoub-wide custom dictionary file.
    /// Located at ~/Radoub/Dictionaries/custom.dic
    /// </summary>
    private readonly string _customDictionaryPath;

    /// <summary>
    /// Whether spell-checking is available and loaded.
    /// Also checks if spell-check is enabled in settings.
    /// </summary>
    public bool IsReady => _isInitialized && _spellChecker != null && DictionarySettingsService.Instance.SpellCheckEnabled && !_isReloading;

    /// <summary>
    /// Event raised when spell-check is ready for use.
    /// </summary>
    public event EventHandler? Ready;

    /// <summary>
    /// Event raised when dictionaries are reloaded (for UI refresh).
    /// </summary>
    public event EventHandler? DictionariesReloaded;

    /// <summary>
    /// Event raised when spell-check enabled state changes.
    /// </summary>
    public event EventHandler<bool>? EnabledChanged;

    private SpellCheckService()
    {
        _dictionaryManager = new DictionaryManager();

        // Setup custom dictionary path: ~/Radoub/Dictionaries/custom.dic
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dictionariesDir = Path.Combine(userProfile, "Radoub", "Dictionaries");
        Directory.CreateDirectory(dictionariesDir);
        _customDictionaryPath = Path.Combine(dictionariesDir, "custom.dic");

        // Subscribe to dictionary settings changes for hot-swap
        DictionarySettingsService.Instance.PrimaryLanguageChanged += OnPrimaryLanguageChanged;
        DictionarySettingsService.Instance.CustomDictionaryToggled += OnCustomDictionaryToggled;
        DictionarySettingsService.Instance.SpellCheckEnabledChanged += OnSpellCheckEnabledChanged;
    }

    /// <summary>
    /// Initialize spell-checking with selected language and enabled dictionaries.
    /// Call this once at app startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _discovery = new DictionaryDiscovery(DictionaryDiscovery.GetDefaultUserDictionaryPath());
            await LoadDictionariesAsync();

            _isInitialized = true;
            Ready?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to initialize spell-check: {ex.Message}");
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Load all dictionaries based on current settings.
    /// </summary>
    private async Task LoadDictionariesAsync()
    {
        var settings = DictionarySettingsService.Instance;
        var primaryLanguage = settings.PrimaryLanguage;

        // Clear discovery cache to pick up any new dictionaries
        _discovery?.ClearCache();

        // Clear user-added words tracking (will be repopulated from file)
        _userAddedWords.Clear();

        // Create fresh dictionary manager and spell checker
        _dictionaryManager = new DictionaryManager();
        _spellChecker?.Dispose();
        _spellChecker = new SpellChecker(_dictionaryManager);

        // Load primary language (Hunspell)
        await LoadPrimaryLanguageAsync(primaryLanguage);

        // Load enabled custom dictionaries
        await LoadEnabledCustomDictionariesAsync();

        // Load user's custom dictionary (words they've added)
        await LoadCustomDictionaryInternalAsync();

        var totalWordCount = GetCustomWordCount();
        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Spell-check loaded: {primaryLanguage} + {_dictionaryManager.DictionaryCount} custom dictionaries ({totalWordCount} words)");
    }

    /// <summary>
    /// Load the primary Hunspell language dictionary.
    /// </summary>
    private async Task LoadPrimaryLanguageAsync(string languageCode)
    {
        if (_spellChecker == null || _discovery == null) return;

        var languages = _discovery.GetAvailableLanguages();
        var langInfo = languages.FirstOrDefault(l => l.Id == languageCode);

        if (langInfo == null)
        {
            // Fallback to bundled en_US
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Language {languageCode} not found, falling back to en_US");
            await _spellChecker.LoadBundledDictionaryAsync("en_US");
            return;
        }

        if (langInfo.IsBundled)
        {
            // Load from embedded resources
            await _spellChecker.LoadBundledDictionaryAsync(languageCode);
        }
        else
        {
            // Load from user's file system
            var affPath = Path.ChangeExtension(langInfo.Path, ".aff");
            await _spellChecker.LoadHunspellDictionaryAsync(langInfo.Path, affPath);
        }
    }

    /// <summary>
    /// Load all enabled custom dictionaries.
    /// </summary>
    private async Task LoadEnabledCustomDictionariesAsync()
    {
        if (_discovery == null) return;

        var settings = DictionarySettingsService.Instance;
        var customDictionaries = _discovery.GetAvailableCustomDictionaries();

        foreach (var dict in customDictionaries)
        {
            if (!settings.IsCustomDictionaryEnabled(dict.Id))
                continue;

            try
            {
                if (dict.IsBundled)
                {
                    // NWN dictionary
                    if (dict.Id == "nwn")
                    {
                        await _spellChecker!.LoadBundledNwnDictionaryAsync();
                    }
                }
                else
                {
                    // User dictionary from file system
                    await _dictionaryManager.LoadDictionaryAsync(dict.Path);
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Loaded custom dictionary: {dict.Id}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load dictionary {dict.Id}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Reload all dictionaries (hot-swap). Called when settings change.
    /// </summary>
    public async Task ReloadDictionariesAsync()
    {
        if (!_isInitialized) return;

        _isReloading = true;

        try
        {
            await LoadDictionariesAsync();
            DictionariesReloaded?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isReloading = false;
        }
    }

    private async void OnPrimaryLanguageChanged(object? sender, string newLanguage)
    {
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Primary language changing to {newLanguage}");
        await ReloadDictionariesAsync();
    }

    private async void OnCustomDictionaryToggled(object? sender, DictionaryToggleEventArgs e)
    {
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Dictionary {e.DictionaryId} {(e.IsEnabled ? "enabled" : "disabled")}");
        await ReloadDictionariesAsync();
    }

    private void OnSpellCheckEnabledChanged(object? sender, bool isEnabled)
    {
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Spell-check {(isEnabled ? "enabled" : "disabled")}");
        EnabledChanged?.Invoke(this, isEnabled);
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
            var trimmedWord = word.Trim();
            _dictionaryManager.AddWord(trimmedWord);
            _userAddedWords.Add(trimmedWord);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added '{trimmedWord}' to custom dictionary");

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
            // Load the file to get words
            var json = await File.ReadAllTextAsync(_customDictionaryPath);
            var dict = System.Text.Json.JsonSerializer.Deserialize<CustomDictionary>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

            if (dict != null)
            {
                // Track user-added words separately
                foreach (var word in dict.AllWords)
                {
                    if (!string.IsNullOrWhiteSpace(word))
                    {
                        _userAddedWords.Add(word.Trim());
                        _dictionaryManager.AddWord(word.Trim());
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Loaded custom dictionary: {_userAddedWords.Count} user words");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load custom dictionary: {ex.Message}");
        }
    }

    /// <summary>
    /// Save the custom dictionary to disk (only user-added words).
    /// </summary>
    public async Task SaveCustomDictionaryAsync()
    {
        try
        {
            var dict = new CustomDictionary
            {
                Source = "Radoub Custom Dictionary",
                Description = "User-added custom words for spell checking",
                Words = _userAddedWords.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(dict,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });

            await File.WriteAllTextAsync(_customDictionaryPath, json);

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Saved custom dictionary: {_userAddedWords.Count} user words");
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
            // Unsubscribe from events
            DictionarySettingsService.Instance.PrimaryLanguageChanged -= OnPrimaryLanguageChanged;
            DictionarySettingsService.Instance.CustomDictionaryToggled -= OnCustomDictionaryToggled;
            DictionarySettingsService.Instance.SpellCheckEnabledChanged -= OnSpellCheckEnabledChanged;

            _spellChecker?.Dispose();
            _spellChecker = null;
            _disposed = true;
        }
    }
}
