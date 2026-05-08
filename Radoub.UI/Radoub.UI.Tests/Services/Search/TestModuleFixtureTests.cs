using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class TestModuleFixtureTests : IDisposable
{
    private readonly string _root;

    public TestModuleFixtureTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void CreateMinimalModule_ProducesExpectedFiles()
    {
        var dir = TestModuleFixture.CreateMinimalModule(_root);
        var files = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();

        Assert.Contains("louis_roumain.utc", files);
        Assert.Contains("louis_dlg.dlg", files);
        Assert.Contains("area01.git", files);
        Assert.Contains("area01.are", files);
        Assert.Contains("script1.nss", files);
    }

    [Fact]
    public void CreateMinimalModule_BuildsDeterministicGffFiles()
    {
        // GffWriter is deterministic (no timestamps/GUIDs, fields written in declaration order)
        // — building the same module twice produces byte-identical files for each name.
        var dir1 = TestModuleFixture.CreateMinimalModule(_root);
        var dir2 = TestModuleFixture.CreateMinimalModule(_root);

        foreach (var file in new[] { "louis_roumain.utc", "louis_dlg.dlg", "area01.git", "area01.are", "script1.nss" })
        {
            var bytes1 = File.ReadAllBytes(Path.Combine(dir1, file));
            var bytes2 = File.ReadAllBytes(Path.Combine(dir2, file));
            Assert.Equal(bytes1, bytes2);
        }
    }
}
