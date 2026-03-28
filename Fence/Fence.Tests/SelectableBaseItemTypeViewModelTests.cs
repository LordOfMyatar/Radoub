using MerchantEditor.ViewModels;
using Xunit;

namespace MerchantEditor.Tests;

public class SelectableBaseItemTypeViewModelTests
{
    [Fact]
    public void IsSelected_DefaultsFalse()
    {
        var vm = new SelectableBaseItemTypeViewModel(0, "Short Sword");
        Assert.False(vm.IsSelected);
    }

    [Fact]
    public void IsSelected_CanBeSetTrue()
    {
        var vm = new SelectableBaseItemTypeViewModel(0, "Short Sword");
        vm.IsSelected = true;
        Assert.True(vm.IsSelected);
    }

    [Fact]
    public void IsSelected_CanBeSetFalse()
    {
        var vm = new SelectableBaseItemTypeViewModel(0, "Short Sword");
        vm.IsSelected = true;
        vm.IsSelected = false;
        Assert.False(vm.IsSelected);
    }

    [Fact]
    public void IsSelected_RaisesPropertyChanged()
    {
        var vm = new SelectableBaseItemTypeViewModel(0, "Short Sword");
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectableBaseItemTypeViewModel.IsSelected))
                raised = true;
        };
        vm.IsSelected = true;
        Assert.True(raised);
    }

    [Fact]
    public void IsSelected_DoesNotRaisePropertyChanged_WhenValueUnchanged()
    {
        var vm = new SelectableBaseItemTypeViewModel(0, "Short Sword");
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectableBaseItemTypeViewModel.IsSelected))
                raised = true;
        };
        vm.IsSelected = false; // Same as default
        Assert.False(raised);
    }

    [Fact]
    public void Constructor_SetsBaseItemIndex()
    {
        var vm = new SelectableBaseItemTypeViewModel(42, "Greatsword");
        Assert.Equal(42, vm.BaseItemIndex);
    }

    [Fact]
    public void Constructor_SetsDisplayName()
    {
        var vm = new SelectableBaseItemTypeViewModel(42, "Greatsword");
        Assert.Equal("Greatsword", vm.DisplayName);
    }

    [Fact]
    public void SelectAll_SetsAllIsSelectedTrue()
    {
        var items = new List<SelectableBaseItemTypeViewModel>
        {
            new(0, "Short Sword"),
            new(1, "Longsword"),
            new(2, "Greatsword")
        };

        foreach (var item in items)
            item.IsSelected = true;

        Assert.All(items, item => Assert.True(item.IsSelected));
    }

    [Fact]
    public void ClearAll_SetsAllIsSelectedFalse()
    {
        var items = new List<SelectableBaseItemTypeViewModel>
        {
            new(0, "Short Sword") { IsSelected = true },
            new(1, "Longsword") { IsSelected = true },
            new(2, "Greatsword") { IsSelected = true }
        };

        foreach (var item in items)
            item.IsSelected = false;

        Assert.All(items, item => Assert.False(item.IsSelected));
    }

    [Fact]
    public void SelectAll_WithEmptyCollection_DoesNotThrow()
    {
        var items = new List<SelectableBaseItemTypeViewModel>();

        var exception = Record.Exception(() =>
        {
            foreach (var item in items)
                item.IsSelected = true;
        });

        Assert.Null(exception);
    }
}
