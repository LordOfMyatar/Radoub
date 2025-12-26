using Radoub.Formats.Gff;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests;

public class ItemViewModelTests
{
    [Fact]
    public void Constructor_SetsBasicProperties()
    {
        // Arrange
        var item = new UtiFile
        {
            TemplateResRef = "test_sword",
            Tag = "SWORD_01",
            BaseItem = 1,
            Cost = 100,
            AddCost = 50
        };

        // Act
        var vm = new ItemViewModel(item, "Test Sword", "Longsword", "Enhancement +1");

        // Assert
        Assert.Equal("test_sword", vm.ResRef);
        Assert.Equal("SWORD_01", vm.Tag);
        Assert.Equal("Test Sword", vm.Name);
        Assert.Equal("Longsword", vm.BaseItemName);
        Assert.Equal(1, vm.BaseItem);
        Assert.Equal(150u, vm.Value); // Cost + AddCost
        Assert.Equal("Enhancement +1", vm.PropertiesDisplay);
    }

    [Fact]
    public void Source_DefaultsToBif()
    {
        var item = new UtiFile();
        var vm = new ItemViewModel(item, "Test", "Type", "");

        Assert.Equal(GameResourceSource.Bif, vm.Source);
    }

    [Fact]
    public void Source_CanBeSetViaConstructor()
    {
        var item = new UtiFile();
        var vm = new ItemViewModel(item, "Test", "Type", "", GameResourceSource.Override);

        Assert.Equal(GameResourceSource.Override, vm.Source);
    }

    [Fact]
    public void IsStandard_TrueWhenSourceIsBif()
    {
        var item = new UtiFile();
        var vm = new ItemViewModel(item, "Test", "Type", "", GameResourceSource.Bif);

        Assert.True(vm.IsStandard);
        Assert.False(vm.IsCustom);
    }

    [Fact]
    public void IsCustom_TrueWhenSourceIsOverride()
    {
        var item = new UtiFile();
        var vm = new ItemViewModel(item, "Test", "Type", "", GameResourceSource.Override);

        Assert.False(vm.IsStandard);
        Assert.True(vm.IsCustom);
    }

    [Fact]
    public void IsCustom_TrueWhenSourceIsHak()
    {
        var item = new UtiFile();
        var vm = new ItemViewModel(item, "Test", "Type", "", GameResourceSource.Hak);

        Assert.False(vm.IsStandard);
        Assert.True(vm.IsCustom);
    }

    [Fact]
    public void IsCustom_TrueWhenSourceIsModule()
    {
        var item = new UtiFile();
        var vm = new ItemViewModel(item, "Test", "Type", "", GameResourceSource.Module);

        Assert.False(vm.IsStandard);
        Assert.True(vm.IsCustom);
    }

    [Fact]
    public void IsSelected_DefaultsFalse()
    {
        var item = new UtiFile();
        var vm = new ItemViewModel(item, "Test", "Type", "");

        Assert.False(vm.IsSelected);
    }

    [Fact]
    public void IsSelected_CanBeSet()
    {
        var item = new UtiFile();
        var vm = new ItemViewModel(item, "Test", "Type", "");

        vm.IsSelected = true;

        Assert.True(vm.IsSelected);
    }

    [Fact]
    public void IsSelected_RaisesPropertyChanged()
    {
        var item = new UtiFile();
        var vm = new ItemViewModel(item, "Test", "Type", "");
        var raised = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ItemViewModel.IsSelected))
                raised = true;
        };

        vm.IsSelected = true;

        Assert.True(raised);
    }

    [Fact]
    public void PropertyCount_ReturnsCorrectCount()
    {
        var item = new UtiFile
        {
            Properties = new List<ItemProperty>
            {
                new ItemProperty { PropertyName = 0 },
                new ItemProperty { PropertyName = 1 },
                new ItemProperty { PropertyName = 2 }
            }
        };
        var vm = new ItemViewModel(item, "Test", "Type", "");

        Assert.Equal(3, vm.PropertyCount);
    }

    [Fact]
    public void StackSize_ReturnsItemStackSize()
    {
        var item = new UtiFile { StackSize = 99 };
        var vm = new ItemViewModel(item, "Test", "Type", "");

        Assert.Equal(99, vm.StackSize);
    }

    [Fact]
    public void IsPlot_ReturnsItemPlotFlag()
    {
        var item = new UtiFile { Plot = true };
        var vm = new ItemViewModel(item, "Test", "Type", "");

        Assert.True(vm.IsPlot);
    }

    [Fact]
    public void IsCursed_ReturnsItemCursedFlag()
    {
        var item = new UtiFile { Cursed = true };
        var vm = new ItemViewModel(item, "Test", "Type", "");

        Assert.True(vm.IsCursed);
    }

    [Fact]
    public void Item_ReturnsUnderlyingUtiFile()
    {
        var item = new UtiFile { TemplateResRef = "original" };
        var vm = new ItemViewModel(item, "Test", "Type", "");

        Assert.Same(item, vm.Item);
    }
}
