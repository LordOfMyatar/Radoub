using MerchantEditor.ViewModels;
using Xunit;

namespace MerchantEditor.Tests;

public class VariableValidationTests
{
    [Fact]
    public void HasError_DefaultsFalse()
    {
        var vm = new VariableViewModel();
        Assert.False(vm.HasError);
        Assert.Equal(string.Empty, vm.ErrorMessage);
    }

    [Fact]
    public void HasError_CanBeSet()
    {
        var vm = new VariableViewModel();
        vm.HasError = true;
        vm.ErrorMessage = "Duplicate name";
        Assert.True(vm.HasError);
        Assert.Equal("Duplicate name", vm.ErrorMessage);
    }

    [Fact]
    public void HasError_RaisesPropertyChanged()
    {
        var vm = new VariableViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(VariableViewModel.HasError)) raised = true; };
        vm.HasError = true;
        Assert.True(raised);
    }

    [Fact]
    public void ErrorMessage_RaisesPropertyChanged()
    {
        var vm = new VariableViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(VariableViewModel.ErrorMessage)) raised = true; };
        vm.ErrorMessage = "test";
        Assert.True(raised);
    }
}
