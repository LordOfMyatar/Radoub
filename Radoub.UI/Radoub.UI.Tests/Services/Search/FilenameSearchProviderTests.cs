using Radoub.Formats.Common;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class FilenameSearchProviderTests : IDisposable
{
    private readonly string _tempDir;

    public FilenameSearchProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"filename-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private void Touch(string fileName)
    {
        File.WriteAllText(Path.Combine(_tempDir, fileName), "stub");
    }

    [Fact]
    public void Search_BasicMatch_ReturnsFilenameResult()
    {
        Touch("louis_roumain.dlg");
        Touch("alice.utc");
        Touch("bob.utc");

        var provider = new FilenameSearchProvider();
        var criteria = new SearchCriteria { Pattern = "louis", IncludeFilenameResRef = true };

        var results = provider.Search(_tempDir, criteria);

        Assert.Single(results);
        Assert.Equal("louis_roumain.dlg", results[0].FileName);
        Assert.Equal(ResourceTypes.Dlg, results[0].ResourceType);
    }
}
