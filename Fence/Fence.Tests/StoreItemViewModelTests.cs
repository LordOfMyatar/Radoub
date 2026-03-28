using MerchantEditor.ViewModels;
using Xunit;

namespace MerchantEditor.Tests;

public class StoreItemViewModelTests
{
    [Fact]
    public void Infinite_DefaultsFalse()
    {
        var vm = new StoreItemViewModel();
        Assert.False(vm.Infinite);
    }

    [Fact]
    public void Infinite_RaisesPropertyChanged()
    {
        var vm = new StoreItemViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StoreItemViewModel.Infinite))
                raised = true;
        };
        vm.Infinite = true;
        Assert.True(raised);
    }

    [Fact]
    public void Infinite_DoesNotRaisePropertyChanged_WhenValueUnchanged()
    {
        var vm = new StoreItemViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StoreItemViewModel.Infinite))
                raised = true;
        };
        vm.Infinite = false; // Same as default
        Assert.False(raised);
    }

    [Fact]
    public void ResRef_RaisesPropertyChanged()
    {
        var vm = new StoreItemViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StoreItemViewModel.ResRef))
                raised = true;
        };
        vm.ResRef = "nw_wswls001";
        Assert.True(raised);
    }

    [Fact]
    public void ResRef_DoesNotRaisePropertyChanged_WhenValueUnchanged()
    {
        var vm = new StoreItemViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StoreItemViewModel.ResRef))
                raised = true;
        };
        vm.ResRef = string.Empty; // Same as default
        Assert.False(raised);
    }

    [Fact]
    public void AllProperties_RaisePropertyChanged()
    {
        var vm = new StoreItemViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        vm.ResRef = "test_item";
        vm.Tag = "TEST_TAG";
        vm.DisplayName = "Test Item";
        vm.Infinite = true;
        vm.PanelId = 2;
        vm.BaseItemType = "Short Sword";
        vm.BaseItemIndex = 4;
        vm.BaseValue = 100;
        vm.SellPrice = 150;
        vm.BuyPrice = 50;
        vm.SourceLocation = "BIF";

        Assert.Contains(nameof(StoreItemViewModel.ResRef), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.Tag), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.DisplayName), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.Infinite), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.PanelId), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.BaseItemType), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.BaseItemIndex), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.BaseValue), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.SellPrice), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.BuyPrice), changedProperties);
        Assert.Contains(nameof(StoreItemViewModel.SourceLocation), changedProperties);
    }
}
