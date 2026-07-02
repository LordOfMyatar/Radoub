using RadoubLauncher.ViewModels;
using Xunit;

namespace Trebuchet.Tests;

// Covers ModuleEditorViewModel.AddHakByName — the shared HAK-list append used by both the
// HAK-list "Add" field and the "New HAK → register in module IFO" flow (#2267).
public class ModuleEditorHakListTests
{
    [Fact]
    public void AddHakByName_AddsBareName()
    {
        var vm = new ModuleEditorViewModel();

        var added = vm.AddHakByName("myhak");

        Assert.True(added);
        Assert.Contains("myhak", vm.HakList);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void AddHakByName_StripsHakExtension()
    {
        var vm = new ModuleEditorViewModel();

        vm.AddHakByName("myhak.hak");

        Assert.Contains("myhak", vm.HakList);
        Assert.DoesNotContain("myhak.hak", vm.HakList);
    }

    [Fact]
    public void AddHakByName_IsCaseInsensitiveDuplicate()
    {
        var vm = new ModuleEditorViewModel();
        vm.AddHakByName("myhak");

        var addedAgain = vm.AddHakByName("MyHak.HAK");

        Assert.False(addedAgain);
        Assert.Single(vm.HakList);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".hak")]
    public void AddHakByName_RejectsBlankOrExtensionOnly(string? name)
    {
        var vm = new ModuleEditorViewModel();

        var added = vm.AddHakByName(name);

        Assert.False(added);
        Assert.Empty(vm.HakList);
    }
}
