using System.ComponentModel;
using MerchantEditor.ViewModels;
using Xunit;

namespace MerchantEditor.Tests.ViewModels;

/// <summary>
/// Tests for StoreItemExtrasViewModel (#2153). The VM is a read-only projection of the
/// selected StoreItemViewModel's store-listing fields (sell/buy/infinite/panel) that the
/// StoreItemExtrasPanel binds to. It must mirror the source and stay live when the source
/// changes (e.g. the context-menu Infinite toggle mutates the source VM).
/// </summary>
public class StoreItemExtrasViewModelTests
{
    private static StoreItemViewModel Source() => new()
    {
        ResRef = "longsword",
        DisplayName = "Longsword",
        SellPrice = 100,
        BuyPrice = 50,
        Infinite = false,
        PanelId = 4
    };

    [Fact]
    public void Constructor_ProjectsSourceFields()
    {
        var vm = new StoreItemExtrasViewModel(Source());

        Assert.Equal(100, vm.SellPrice);
        Assert.Equal(50, vm.BuyPrice);
        Assert.False(vm.Infinite);
        Assert.Equal(4, vm.PanelId);
    }

    [Fact]
    public void SourceInfiniteChange_Propagates()
    {
        var source = Source();
        var vm = new StoreItemExtrasViewModel(source);

        source.Infinite = true;

        Assert.True(vm.Infinite);
    }

    [Fact]
    public void SourceInfiniteChange_RaisesPropertyChanged()
    {
        var source = Source();
        var vm = new StoreItemExtrasViewModel(source);
        var raised = new System.Collections.Generic.List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        source.Infinite = true;

        Assert.Contains(nameof(StoreItemExtrasViewModel.Infinite), raised);
    }

    [Fact]
    public void SourcePriceChange_Propagates()
    {
        var source = Source();
        var vm = new StoreItemExtrasViewModel(source);

        source.SellPrice = 250;
        source.BuyPrice = 80;

        Assert.Equal(250, vm.SellPrice);
        Assert.Equal(80, vm.BuyPrice);
    }

    [Fact]
    public void SourcePanelChange_Propagates()
    {
        var source = Source();
        var vm = new StoreItemExtrasViewModel(source);

        source.PanelId = 0;

        Assert.Equal(0, vm.PanelId);
    }

    [Fact]
    public void Detach_StopsRaisingPropertyChanged()
    {
        var source = Source();
        var vm = new StoreItemExtrasViewModel(source);
        var raised = new System.Collections.Generic.List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Detach();
        source.Infinite = true;

        // After detach the VM no longer forwards source change notifications.
        Assert.Empty(raised);
    }
}
