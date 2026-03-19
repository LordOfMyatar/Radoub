using ItemEditor.ViewModels;
using Radoub.Formats.Gff;
using Xunit;

namespace ItemEditor.Tests.ViewModels;

public class VariableViewModelTests
{
    [Fact]
    public void FromVariable_Int_MapsCorrectly()
    {
        var variable = Variable.CreateInt("nCount", 42);

        var vm = VariableViewModel.FromVariable(variable);

        Assert.Equal("nCount", vm.Name);
        Assert.Equal(VariableType.Int, vm.Type);
        Assert.Equal(42, vm.IntValue);
        Assert.True(vm.IsIntType);
        Assert.False(vm.IsFloatType);
        Assert.False(vm.IsStringType);
    }

    [Fact]
    public void FromVariable_Float_MapsCorrectly()
    {
        var variable = Variable.CreateFloat("fDamage", 2.5f);

        var vm = VariableViewModel.FromVariable(variable);

        Assert.Equal("fDamage", vm.Name);
        Assert.Equal(VariableType.Float, vm.Type);
        Assert.Equal(2.5m, vm.FloatValue);
        Assert.True(vm.IsFloatType);
    }

    [Fact]
    public void FromVariable_String_MapsCorrectly()
    {
        var variable = Variable.CreateString("sOwner", "Drizzt");

        var vm = VariableViewModel.FromVariable(variable);

        Assert.Equal("sOwner", vm.Name);
        Assert.Equal(VariableType.String, vm.Type);
        Assert.Equal("Drizzt", vm.StringValue);
        Assert.True(vm.IsStringType);
    }

    [Fact]
    public void ToVariable_Int_RoundTrips()
    {
        var vm = new VariableViewModel
        {
            Name = "nHP",
            Type = VariableType.Int,
            IntValue = 100
        };

        var variable = vm.ToVariable();

        Assert.Equal("nHP", variable.Name);
        Assert.Equal(VariableType.Int, variable.Type);
        Assert.Equal(100, variable.GetInt());
    }

    [Fact]
    public void ToVariable_Float_RoundTrips()
    {
        var vm = new VariableViewModel
        {
            Name = "fSpeed",
            Type = VariableType.Float,
            FloatValue = 1.5m
        };

        var variable = vm.ToVariable();

        Assert.Equal("fSpeed", variable.Name);
        Assert.Equal(VariableType.Float, variable.Type);
        Assert.Equal(1.5f, variable.GetFloat(), 0.001f);
    }

    [Fact]
    public void ToVariable_String_RoundTrips()
    {
        var vm = new VariableViewModel
        {
            Name = "sTag",
            Type = VariableType.String,
            StringValue = "my_item"
        };

        var variable = vm.ToVariable();

        Assert.Equal("sTag", variable.Name);
        Assert.Equal(VariableType.String, variable.Type);
        Assert.Equal("my_item", variable.GetString());
    }

    [Fact]
    public void TypeIndex_MapsCorrectly()
    {
        var vm = new VariableViewModel();

        vm.TypeIndex = 0;
        Assert.Equal(VariableType.Int, vm.Type);

        vm.TypeIndex = 1;
        Assert.Equal(VariableType.Float, vm.Type);

        vm.TypeIndex = 2;
        Assert.Equal(VariableType.String, vm.Type);
    }

    [Fact]
    public void TypeDisplay_ShowsCorrectStrings()
    {
        var vm = new VariableViewModel();

        vm.Type = VariableType.Int;
        Assert.Equal("Int", vm.TypeDisplay);

        vm.Type = VariableType.Float;
        Assert.Equal("Float", vm.TypeDisplay);

        vm.Type = VariableType.String;
        Assert.Equal("String", vm.TypeDisplay);
    }

    [Fact]
    public void ValueDisplay_ShowsCorrectFormat()
    {
        var vm = new VariableViewModel { Type = VariableType.Int, IntValue = 42 };
        Assert.Equal("42", vm.ValueDisplay);

        vm = new VariableViewModel { Type = VariableType.String, StringValue = "hello" };
        Assert.Equal("hello", vm.ValueDisplay);
    }

    [Fact]
    public void PropertyChanged_RaisedOnNameChange()
    {
        var vm = new VariableViewModel();
        var changedProperties = new System.Collections.Generic.List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName != null) changedProperties.Add(e.PropertyName); };

        vm.Name = "test";

        Assert.Contains(nameof(VariableViewModel.Name), changedProperties);
        Assert.Contains(nameof(VariableViewModel.HasEmptyName), changedProperties);
    }

    [Fact]
    public void ToVariable_EmptyName_StillCreatesVariable()
    {
        // Validation happens at the UI layer, not the ViewModel
        var vm = new VariableViewModel
        {
            Name = "",
            Type = VariableType.Int,
            IntValue = 0
        };

        var variable = vm.ToVariable();
        Assert.Equal("", variable.Name);
    }

    [Fact]
    public void FromVariable_AndBackToVariable_PreservesData()
    {
        var original = Variable.CreateFloat("fMultiplier", 1.234f);

        var vm = VariableViewModel.FromVariable(original);
        var result = vm.ToVariable();

        Assert.Equal(original.Name, result.Name);
        Assert.Equal(original.Type, result.Type);
        Assert.Equal(original.GetFloat(), result.GetFloat(), 0.001f);
    }

    [Fact]
    public void HasEmptyName_TrueWhenEmpty()
    {
        var vm = new VariableViewModel { Name = "" };
        Assert.True(vm.HasEmptyName);
    }

    [Fact]
    public void HasEmptyName_FalseWhenSet()
    {
        var vm = new VariableViewModel { Name = "nCount" };
        Assert.False(vm.HasEmptyName);
    }

    [Fact]
    public void HasError_DefaultsFalse()
    {
        var vm = new VariableViewModel();
        Assert.False(vm.HasError);
        Assert.Equal(string.Empty, vm.ErrorMessage);
    }

    [Fact]
    public void HasError_CanBeSetExternally()
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
        string? changed = null;
        vm.PropertyChanged += (_, e) => changed = e.PropertyName;

        vm.HasError = true;

        Assert.Equal(nameof(VariableViewModel.HasError), changed);
    }

    [Fact]
    public void TypeChange_RaisesMultiplePropertyChanged()
    {
        var vm = new VariableViewModel { Type = VariableType.Int };
        var changedProperties = new System.Collections.Generic.List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName != null) changedProperties.Add(e.PropertyName); };

        vm.Type = VariableType.String;

        Assert.Contains(nameof(VariableViewModel.Type), changedProperties);
        Assert.Contains(nameof(VariableViewModel.TypeDisplay), changedProperties);
        Assert.Contains(nameof(VariableViewModel.ValueDisplay), changedProperties);
        Assert.Contains(nameof(VariableViewModel.IsIntType), changedProperties);
        Assert.Contains(nameof(VariableViewModel.IsStringType), changedProperties);
    }
}
