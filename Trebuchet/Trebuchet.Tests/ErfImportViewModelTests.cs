using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using RadoubLauncher.ViewModels;
using Xunit;

namespace Trebuchet.Tests;

public class ErfImportViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _targetDir;
    private readonly string _erfPath;

    public ErfImportViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ErfVmTest_" + Guid.NewGuid().ToString("N")[..8]);
        _targetDir = Path.Combine(_tempDir, "module");
        Directory.CreateDirectory(_targetDir);
        _erfPath = Path.Combine(_tempDir, "test.erf");

        CreateTestErf();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void CreateTestErf()
    {
        var erf = new ErfFile { FileType = "ERF ", FileVersion = "V1.0" };
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();

        AddResource(erf, resourceData, "script_01", ResourceTypes.Ncs, new byte[] { 0x01, 0x02 });
        AddResource(erf, resourceData, "script_02", ResourceTypes.Ncs, new byte[] { 0x03, 0x04 });
        AddResource(erf, resourceData, "item_01", ResourceTypes.Uti, new byte[] { 0x05, 0x06 });
        AddResource(erf, resourceData, "store_01", ResourceTypes.Utm, new byte[] { 0x07, 0x08 });

        ErfWriter.Write(erf, _erfPath, resourceData);
    }

    private static void AddResource(ErfFile erf, Dictionary<(string ResRef, ushort Type), byte[]> data,
        string resRef, ushort type, byte[] content)
    {
        erf.Resources.Add(new ErfResourceEntry
        {
            ResRef = resRef,
            ResourceType = type,
            ResId = (uint)erf.Resources.Count,
            Size = (uint)content.Length
        });
        data[(resRef, type)] = content;
    }

    #region LoadErf

    [Fact]
    public async Task LoadErf_PopulatesResources()
    {
        var vm = new ErfImportViewModel(_targetDir);

        await vm.LoadErfAsync(_erfPath);

        Assert.True(vm.IsErfLoaded);
        Assert.Equal(4, vm.TotalCount);
        Assert.Equal(4, vm.FilteredResources.Count);
    }

    [Fact]
    public async Task LoadErf_DetectsConflicts()
    {
        File.WriteAllBytes(Path.Combine(_targetDir, "item_01.uti"), new byte[] { 0x00 });
        var vm = new ErfImportViewModel(_targetDir);

        await vm.LoadErfAsync(_erfPath);

        var conflicting = vm.FilteredResources.Where(r => r.ExistsInModule).ToList();
        Assert.Single(conflicting);
        Assert.Equal("item_01", conflicting[0].ResRef);
    }

    [Fact]
    public async Task LoadErf_PopulatesTypeFilters()
    {
        var vm = new ErfImportViewModel(_targetDir);

        await vm.LoadErfAsync(_erfPath);

        Assert.Contains("All Types", vm.TypeFilters);
        Assert.Contains("Script (compiled)", vm.TypeFilters);
        Assert.Contains("Item", vm.TypeFilters);
        Assert.Contains("Store", vm.TypeFilters);
    }

    #endregion

    #region Filtering

    [Fact]
    public async Task SearchFilter_ByResRef_FiltersCorrectly()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);

        vm.SearchText = "script";

        Assert.Equal(2, vm.FilteredResources.Count);
        Assert.All(vm.FilteredResources, r => Assert.Contains("script", r.ResRef));
    }

    [Fact]
    public async Task TypeFilter_ByType_FiltersCorrectly()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);

        vm.SelectedTypeFilter = "Item";

        Assert.Single(vm.FilteredResources);
        Assert.Equal("item_01", vm.FilteredResources[0].ResRef);
    }

    [Fact]
    public async Task CombinedFilter_SearchAndType_FiltersCorrectly()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);

        vm.SelectedTypeFilter = "Script (compiled)";
        vm.SearchText = "02";

        Assert.Single(vm.FilteredResources);
        Assert.Equal("script_02", vm.FilteredResources[0].ResRef);
    }

    [Fact]
    public async Task SearchFilter_CaseInsensitive()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);

        vm.SearchText = "SCRIPT";

        Assert.Equal(2, vm.FilteredResources.Count);
    }

    #endregion

    #region Selection

    [Fact]
    public async Task AllResourcesSelectedByDefault()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);

        Assert.Equal(4, vm.SelectedCount);
    }

    [Fact]
    public async Task DeselectAll_DeselectsAll()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);

        vm.DeselectAll();

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.CanImport);
    }

    [Fact]
    public async Task SelectAll_AfterDeselect_SelectsAll()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);
        vm.DeselectAll();

        vm.SelectAll();

        Assert.Equal(4, vm.SelectedCount);
    }

    [Fact]
    public async Task SelectAll_RespectsFilter()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);
        vm.DeselectAll();
        vm.SearchText = "script";

        vm.SelectAll();

        // Only filtered items should be selected
        Assert.Equal(2, vm.SelectedCount);
    }

    #endregion

    #region Import

    [Fact]
    public async Task Import_CreatesFilesInTargetDirectory()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);

        var result = await vm.ImportAsync();

        Assert.NotNull(result);
        Assert.Equal(4, result.ImportedCount);
        Assert.True(File.Exists(Path.Combine(_targetDir, "script_01.ncs")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "item_01.uti")));
    }

    [Fact]
    public async Task CanImport_FalseWhenNoResourcesSelected()
    {
        var vm = new ErfImportViewModel(_targetDir);
        await vm.LoadErfAsync(_erfPath);
        vm.DeselectAll();

        Assert.False(vm.CanImport);
    }

    [Fact]
    public async Task CanImport_FalseWhenNotLoaded()
    {
        var vm = new ErfImportViewModel(_targetDir);

        Assert.False(vm.CanImport);
    }

    #endregion
}
