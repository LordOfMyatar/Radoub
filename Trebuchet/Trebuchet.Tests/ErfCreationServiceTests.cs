using System;
using System.IO;
using Radoub.Formats.Erf;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

public class ErfCreationServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ErfCreationService _service;

    public ErfCreationServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ErfCreateTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _service = new ErfCreationService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void CreateErf_WritesFileToDisk()
    {
        var path = Path.Combine(_tempDir, "myarchive.erf");

        _service.CreateErf(path);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CreateErf_ProducesEmptyErfWithCorrectFileType()
    {
        var path = Path.Combine(_tempDir, "myarchive.erf");

        _service.CreateErf(path);

        var erf = ErfReader.Read(path);
        Assert.Equal("ERF ", erf.FileType);
        Assert.Equal("V1.0", erf.FileVersion);
        Assert.Empty(erf.Resources);
    }

    [Fact]
    public void CreateErf_WithDescription_StoresLocalizedString()
    {
        var path = Path.Combine(_tempDir, "myarchive.erf");

        _service.CreateErf(path, description: "My test archive");

        var erf = ErfReader.Read(path);
        Assert.Contains(erf.LocalizedStrings, s => s.Text == "My test archive");
    }

    [Fact]
    public void CreateErf_RejectsInvalidAuroraFilename()
    {
        // 17-char stem exceeds Aurora's 16-char limit
        var path = Path.Combine(_tempDir, "thisnameistoolong.erf");

        var ex = Assert.Throws<ArgumentException>(() => _service.CreateErf(path));
        Assert.Contains("16", ex.Message);
    }

    [Fact]
    public void CreateErf_RejectsIllegalCharacters()
    {
        var path = Path.Combine(_tempDir, "bad-name!.erf");

        Assert.Throws<ArgumentException>(() => _service.CreateErf(path));
    }

    [Fact]
    public void CreateErf_DoesNotOverwriteExistingByDefault()
    {
        var path = Path.Combine(_tempDir, "exists.erf");
        File.WriteAllText(path, "preexisting");

        Assert.Throws<IOException>(() => _service.CreateErf(path));
    }
}
