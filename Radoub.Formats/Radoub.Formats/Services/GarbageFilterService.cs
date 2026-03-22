using Radoub.Formats.Settings;

namespace Radoub.Formats.Services;

/// <summary>
/// User-configurable garbage label filter for 2DA entries.
/// Reads filter patterns from RadoubSettings.GarbageFilters.
/// Patterns: bare string = case-insensitive substring, "=" prefix = case-insensitive exact.
/// </summary>
public class GarbageFilterService
{
    private static GarbageFilterService? _instance;
    private static readonly object _lock = new();

    private readonly List<string> _substringPatterns = new();
    private readonly List<string> _exactPatterns = new();
    private readonly List<string> _rawFilters;

    /// <summary>
    /// Singleton instance backed by RadoubSettings.
    /// </summary>
    public static GarbageFilterService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new GarbageFilterService(RadoubSettings.Instance.GarbageFilters);
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Create with explicit filter list (for testing or custom scenarios).
    /// </summary>
    public GarbageFilterService(IReadOnlyList<string> filters)
    {
        _rawFilters = filters.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();

        foreach (var filter in _rawFilters)
        {
            if (filter.StartsWith('='))
            {
                var pattern = filter.Substring(1);
                if (!string.IsNullOrWhiteSpace(pattern))
                    _exactPatterns.Add(pattern);
            }
            else
            {
                _substringPatterns.Add(filter);
            }
        }
    }

    /// <summary>
    /// Check if a label matches any garbage filter pattern.
    /// Returns true for null, empty, or whitespace-only labels.
    /// </summary>
    public bool IsGarbageLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return true;

        foreach (var pattern in _substringPatterns)
        {
            if (label.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var pattern in _exactPatterns)
        {
            if (label.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get the current filter list (raw patterns including "=" prefixes).
    /// </summary>
    public IReadOnlyList<string> GetFilters() => _rawFilters.AsReadOnly();

    /// <summary>
    /// Reset singleton (for testing or after settings reload).
    /// </summary>
    public static void ResetInstance()
    {
        lock (_lock)
        {
            _instance = null;
        }
    }
}
