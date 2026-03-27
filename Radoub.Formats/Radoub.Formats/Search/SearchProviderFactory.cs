namespace Radoub.Formats.Search;

/// <summary>
/// Maps resource types to their search providers.
/// </summary>
public class SearchProviderFactory
{
    private readonly Dictionary<ushort, IFileSearchProvider> _providers = new();
    private IFileSearchProvider? _fallback;

    public void Register(IFileSearchProvider provider)
    {
        _providers[provider.FileType] = provider;
    }

    public void SetFallback(IFileSearchProvider fallback)
    {
        _fallback = fallback;
    }

    /// <summary>
    /// Get the provider for a resource type.
    /// Returns the fallback (GenericGffSearchProvider) if no dedicated provider exists.
    /// Returns null if no provider and no fallback.
    /// </summary>
    public IFileSearchProvider? GetProvider(ushort resourceType)
    {
        return _providers.TryGetValue(resourceType, out var provider)
            ? provider
            : _fallback;
    }

    /// <summary>
    /// Returns true if a dedicated (non-fallback) provider exists for this type.
    /// </summary>
    public bool HasDedicatedProvider(ushort resourceType)
    {
        return _providers.ContainsKey(resourceType);
    }

    /// <summary>
    /// Create a fully initialized factory with all built-in providers.
    /// Called at startup by ModuleSearchService.
    /// </summary>
    public static SearchProviderFactory CreateDefault()
    {
        var factory = new SearchProviderFactory();
        factory.Register(new DlgSearchProvider());
        factory.Register(new UtcSearchProvider());
        factory.Register(new UtiSearchProvider());
        factory.Register(new UtmSearchProvider());
        factory.Register(new UtpSearchProvider());
        factory.Register(new UtdSearchProvider());
        factory.Register(new JrlSearchProvider());
        factory.Register(new AreSearchProvider());
        factory.Register(new GitSearchProvider());
        factory.SetFallback(new GenericGffSearchProvider());
        return factory;
    }
}
