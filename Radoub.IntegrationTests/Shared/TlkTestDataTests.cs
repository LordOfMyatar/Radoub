using Radoub.Formats.Tlk;
using Radoub.IntegrationTests.Tools;
using Xunit;

namespace Radoub.IntegrationTests.Shared;

/// <summary>
/// Tests for TLK test data generation and usage.
/// These tests verify the test TLK files exist and are readable.
/// </summary>
public class TlkTestDataTests
{
    [Fact]
    public void TestTlkFiles_Exist()
    {
        var dialogTlk = Path.Combine(TestPaths.TestGameRoot, "data", "dialog.tlk");
        var customTlk = Path.Combine(TestPaths.TestDataRoot, "UserRoot", "tlk", "custom.tlk");

        // If files don't exist, generate them
        if (!File.Exists(dialogTlk) || !File.Exists(customTlk))
        {
            TlkTestDataGenerator.GenerateTestTlkFiles(TestPaths.TestDataRoot);
        }

        Assert.True(File.Exists(dialogTlk), $"dialog.tlk should exist at {dialogTlk}");
        Assert.True(File.Exists(customTlk), $"custom.tlk should exist at {customTlk}");
    }

    [Fact]
    public void DialogTlk_CanBeRead()
    {
        var dialogTlk = Path.Combine(TestPaths.TestGameRoot, "data", "dialog.tlk");

        // Generate if needed
        if (!File.Exists(dialogTlk))
        {
            TlkTestDataGenerator.GenerateTestTlkFiles(TestPaths.TestDataRoot);
        }

        var tlk = TlkReader.Read(dialogTlk);

        Assert.NotNull(tlk);
        Assert.True(tlk.Count > 0, "dialog.tlk should have entries");
        Assert.Equal("TLK ", tlk.FileType);
    }

    [Fact]
    public void DialogTlk_ContainsExpectedStrings()
    {
        var dialogTlk = Path.Combine(TestPaths.TestGameRoot, "data", "dialog.tlk");

        // Generate if needed
        if (!File.Exists(dialogTlk))
        {
            TlkTestDataGenerator.GenerateTestTlkFiles(TestPaths.TestDataRoot);
        }

        var tlk = TlkReader.Read(dialogTlk);

        // Verify some expected strings
        Assert.Equal("Human", tlk.GetString(10));
        Assert.Equal("Strength", tlk.GetString(20));
        Assert.Equal("Longsword", tlk.GetString(1000));
    }

    [Fact]
    public void CustomTlk_CanBeRead()
    {
        var customTlk = Path.Combine(TestPaths.TestDataRoot, "UserRoot", "tlk", "custom.tlk");

        // Generate if needed
        if (!File.Exists(customTlk))
        {
            TlkTestDataGenerator.GenerateTestTlkFiles(TestPaths.TestDataRoot);
        }

        var tlk = TlkReader.Read(customTlk);

        Assert.NotNull(tlk);
        Assert.True(tlk.Count > 0, "custom.tlk should have entries");
    }

    [Fact]
    public void CustomTlk_ContainsExpectedStrings()
    {
        var customTlk = Path.Combine(TestPaths.TestDataRoot, "UserRoot", "tlk", "custom.tlk");

        // Generate if needed
        if (!File.Exists(customTlk))
        {
            TlkTestDataGenerator.GenerateTestTlkFiles(TestPaths.TestDataRoot);
        }

        var tlk = TlkReader.Read(customTlk);

        // Verify custom content strings
        Assert.Equal("Custom Race 1", tlk.GetString(0));
        Assert.Equal("Custom Class", tlk.GetString(10));
        Assert.Equal("CEP Longsword +1", tlk.GetString(100));
    }

    /// <summary>
    /// This test can be used to regenerate test TLK files if needed.
    /// Run with: dotnet test --filter "FullyQualifiedName~RegenerateTlkFiles"
    /// </summary>
    [Fact]
    [Trait("Category", "Generator")]
    public void RegenerateTlkFiles()
    {
        TlkTestDataGenerator.GenerateTestTlkFiles(TestPaths.TestDataRoot);

        var dialogTlk = Path.Combine(TestPaths.TestGameRoot, "data", "dialog.tlk");
        var customTlk = Path.Combine(TestPaths.TestDataRoot, "UserRoot", "tlk", "custom.tlk");

        Assert.True(File.Exists(dialogTlk));
        Assert.True(File.Exists(customTlk));
    }
}
