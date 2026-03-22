using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search;

public class SearchProviderFactoryTests
{
    private class FakeProvider : IFileSearchProvider
    {
        public ushort FileType { get; init; }
        public IReadOnlyList<string> Extensions => new[] { ".fake" };
        public IReadOnlyList<SearchMatch> Search(GffFile gffFile, SearchCriteria criteria) => Array.Empty<SearchMatch>();
        public IReadOnlyList<ReplaceResult> Replace(GffFile gffFile, IReadOnlyList<ReplaceOperation> operations) => Array.Empty<ReplaceResult>();
    }

    [Fact]
    public void GetProvider_RegisteredType_ReturnsDedicatedProvider()
    {
        var factory = new SearchProviderFactory();
        var provider = new FakeProvider { FileType = ResourceTypes.Dlg };
        factory.Register(provider);
        Assert.Same(provider, factory.GetProvider(ResourceTypes.Dlg));
    }

    [Fact]
    public void GetProvider_UnregisteredType_ReturnsFallback()
    {
        var factory = new SearchProviderFactory();
        var fallback = new FakeProvider { FileType = 0 };
        factory.SetFallback(fallback);
        Assert.Same(fallback, factory.GetProvider(ResourceTypes.Utc));
    }

    [Fact]
    public void GetProvider_NoProviderNoFallback_ReturnsNull()
    {
        var factory = new SearchProviderFactory();
        Assert.Null(factory.GetProvider(ResourceTypes.Utc));
    }

    [Fact]
    public void HasDedicatedProvider_Registered_ReturnsTrue()
    {
        var factory = new SearchProviderFactory();
        factory.Register(new FakeProvider { FileType = ResourceTypes.Dlg });
        Assert.True(factory.HasDedicatedProvider(ResourceTypes.Dlg));
    }

    [Fact]
    public void HasDedicatedProvider_Unregistered_ReturnsFalse()
    {
        var factory = new SearchProviderFactory();
        Assert.False(factory.HasDedicatedProvider(ResourceTypes.Dlg));
    }

    [Fact]
    public void CreateDefault_ReturnsDlgProvider()
    {
        var factory = SearchProviderFactory.CreateDefault();
        var provider = factory.GetProvider(ResourceTypes.Dlg);
        Assert.NotNull(provider);
        Assert.True(factory.HasDedicatedProvider(ResourceTypes.Dlg));
    }

    [Fact]
    public void CreateDefault_ReturnsGenericFallback()
    {
        var factory = SearchProviderFactory.CreateDefault();
        // Any unregistered type should get the fallback
        var provider = factory.GetProvider(ResourceTypes.Utc);
        Assert.NotNull(provider);
        Assert.False(factory.HasDedicatedProvider(ResourceTypes.Utc));
    }
}
