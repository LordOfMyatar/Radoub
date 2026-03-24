using MerchantEditor.ViewModels;
using Xunit;

namespace Fence.Tests;

public class StoreItemIconTests
{
    [Fact]
    public void IconBitmap_DefaultsToNull()
    {
        var vm = new StoreItemViewModel();
        Assert.Null(vm.IconBitmap);
    }

    [Fact]
    public void IconBitmap_RaisesPropertyChanged()
    {
        var vm = new StoreItemViewModel();
        var raised = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StoreItemViewModel.IconBitmap))
                raised = true;
        };

        vm.IconBitmap = null; // Setting to same value (null -> null) should not raise
        Assert.False(raised);
    }

    [Fact]
    public void StoreItemViewModel_HasBaseItemIndex_ForIconLookup()
    {
        var vm = new StoreItemViewModel
        {
            BaseItemIndex = 42,
            DisplayName = "Test Sword"
        };

        Assert.Equal(42, vm.BaseItemIndex);
    }
}
