using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

public class ModuleCreationServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _templatePath;
    private readonly ModuleCreationService _service;

    public ModuleCreationServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ModCreateTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _templatePath = Path.Combine(_tempDir, "blank.mod");
        _service = new ModuleCreationService();

        CreateTemplateMod();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    /// <summary>
    /// Build a minimal blank-forest template .mod mirroring the real blank.mod:
    /// one area (.are/.git/.gic), module.ifo, Repute.fac, and palette .itp stubs.
    /// </summary>
    private void CreateTemplateMod()
    {
        var erf = new ErfFile { FileType = "MOD ", FileVersion = "V1.0" };
        var data = new Dictionary<(string ResRef, ushort Type), byte[]>();

        Add(erf, data, "area001", ResourceTypes.Are, GffStub());
        Add(erf, data, "area001", ResourceTypes.Git, GffStub());
        Add(erf, data, "area001", ResourceTypes.Gic, GffStub());
        Add(erf, data, "module", ResourceTypes.Ifo, GffStub());
        Add(erf, data, "Repute", ResourceTypes.Fac, GffStub());
        Add(erf, data, "creaturepalcus", ResourceTypes.Itp, GffStub());

        ErfWriter.Write(erf, _templatePath, data);
    }

    private static byte[] GffStub() => new byte[] { 0x47, 0x46, 0x46, 0x20, 0x56, 0x33, 0x2E, 0x32 }; // "GFF V3.2"

    private static void Add(ErfFile erf, Dictionary<(string, ushort), byte[]> data,
        string resRef, ushort type, byte[] content)
    {
        erf.Resources.Add(new ErfResourceEntry { ResRef = resRef, ResourceType = type, ResId = (uint)erf.Resources.Count });
        data[(resRef.ToLowerInvariant(), type)] = content;
    }

    [Fact]
    public void CreateModule_WritesFileToDisk()
    {
        var outPath = Path.Combine(_tempDir, "mymodule.mod");

        _service.CreateModule(outPath, _templatePath);

        Assert.True(File.Exists(outPath));
    }

    [Fact]
    public void CreateModule_ProducesModFileType()
    {
        var outPath = Path.Combine(_tempDir, "mymodule.mod");

        _service.CreateModule(outPath, _templatePath);

        var mod = ErfReader.Read(outPath);
        Assert.Equal("MOD ", mod.FileType);
        Assert.Equal("V1.0", mod.FileVersion);
    }

    [Fact]
    public void CreateModule_SeedsModuleIfo()
    {
        var outPath = Path.Combine(_tempDir, "mymodule.mod");

        _service.CreateModule(outPath, _templatePath);

        var mod = ErfReader.Read(outPath);
        Assert.Contains(mod.Resources,
            r => r.ResRef.Equals("module", StringComparison.OrdinalIgnoreCase) && r.ResourceType == ResourceTypes.Ifo);
    }

    [Fact]
    public void CreateModule_SeedsBlankForestArea()
    {
        var outPath = Path.Combine(_tempDir, "mymodule.mod");

        _service.CreateModule(outPath, _templatePath);

        var mod = ErfReader.Read(outPath);
        Assert.Contains(mod.Resources,
            r => r.ResRef.Equals("area001", StringComparison.OrdinalIgnoreCase) && r.ResourceType == ResourceTypes.Are);
    }

    [Fact]
    public void CreateModule_PreservesAllTemplateResources()
    {
        var outPath = Path.Combine(_tempDir, "mymodule.mod");
        var template = ErfReader.Read(_templatePath);

        _service.CreateModule(outPath, _templatePath);

        var mod = ErfReader.Read(outPath);
        Assert.Equal(template.Resources.Count, mod.Resources.Count);
    }

    [Fact]
    public void CreateModule_PreservesResourceContent()
    {
        var outPath = Path.Combine(_tempDir, "mymodule.mod");

        _service.CreateModule(outPath, _templatePath);

        // module.ifo content survives the copy intact
        var mod = ErfReader.Read(outPath);
        var ifo = mod.FindResource("module", ResourceTypes.Ifo);
        Assert.NotNull(ifo);
        var bytes = ErfReader.ExtractResource(outPath, ifo!);
        Assert.Equal(GffStub(), bytes);
    }

    [Fact]
    public void CreateModule_RejectsInvalidAuroraFilename()
    {
        var outPath = Path.Combine(_tempDir, "thisnameistoolong.mod");

        var ex = Assert.Throws<ArgumentException>(() => _service.CreateModule(outPath, _templatePath));
        Assert.Contains("16", ex.Message);
    }

    [Fact]
    public void CreateModule_ThrowsWhenTemplateMissing()
    {
        var outPath = Path.Combine(_tempDir, "mymodule.mod");
        var missing = Path.Combine(_tempDir, "nope.mod");

        Assert.Throws<FileNotFoundException>(() => _service.CreateModule(outPath, missing));
    }

    [Fact]
    public void CreateModule_DoesNotOverwriteExistingByDefault()
    {
        var outPath = Path.Combine(_tempDir, "exists.mod");
        File.WriteAllText(outPath, "preexisting");

        Assert.Throws<IOException>(() => _service.CreateModule(outPath, _templatePath));
    }
}
