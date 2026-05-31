using System.Collections.Generic;
using Radoub.Formats.Gff;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests.ViewModels;

/// <summary>
/// Tests for the shared <see cref="VariableViewModel"/> (superset of the four tool-local
/// VMs). Ported from Relique/QM/Fence tests plus new coverage for object/location types
/// and NWN name-rule validation (#2293).
/// </summary>
public class VariableViewModelTests
{
    // --- FromVariable / type mapping (all 5 types) ---

    [Fact]
    public void FromVariable_Int_MapsCorrectly()
    {
        var vm = VariableViewModel.FromVariable(Variable.CreateInt("nCount", 42));

        Assert.Equal("nCount", vm.Name);
        Assert.Equal(VariableType.Int, vm.Type);
        Assert.Equal(42, vm.IntValue);
        Assert.True(vm.IsIntType);
        Assert.False(vm.IsFloatType);
    }

    [Fact]
    public void FromVariable_Float_MapsCorrectly()
    {
        var vm = VariableViewModel.FromVariable(Variable.CreateFloat("fDamage", 2.5f));

        Assert.Equal("fDamage", vm.Name);
        Assert.Equal(VariableType.Float, vm.Type);
        Assert.Equal(2.5m, vm.FloatValue);
        Assert.True(vm.IsFloatType);
    }

    [Fact]
    public void FromVariable_String_MapsCorrectly()
    {
        var vm = VariableViewModel.FromVariable(Variable.CreateString("sOwner", "Drizzt"));

        Assert.Equal("sOwner", vm.Name);
        Assert.Equal(VariableType.String, vm.Type);
        Assert.Equal("Drizzt", vm.StringValue);
        Assert.True(vm.IsStringType);
    }

    [Fact]
    public void FromVariable_Object_MapsCorrectly()
    {
        var vm = VariableViewModel.FromVariable(Variable.CreateObject("oTarget", 0x1234u));

        Assert.Equal("oTarget", vm.Name);
        Assert.Equal(VariableType.Object, vm.Type);
        Assert.Equal(0x1234u, vm.ObjectIdValue);
        Assert.True(vm.IsObjectType);
    }

    [Fact]
    public void FromVariable_Location_MapsCorrectly()
    {
        var loc = new VariableLocation
        {
            Area = 7, PositionX = 1.5f, PositionY = 2.5f, PositionZ = 3.5f,
            OrientationX = 0.1f, OrientationY = 0.2f, OrientationZ = 0.3f
        };

        var vm = VariableViewModel.FromVariable(Variable.CreateLocation("lSpawn", loc));

        Assert.Equal("lSpawn", vm.Name);
        Assert.Equal(VariableType.Location, vm.Type);
        Assert.True(vm.IsLocationType);
        Assert.Equal(7u, vm.LocationArea);
        Assert.Equal(1.5f, vm.LocationPositionX, 0.001f);
        Assert.Equal(3.5f, vm.LocationPositionZ, 0.001f);
        Assert.Equal(0.3f, vm.LocationOrientationZ, 0.001f);
    }

    // --- ToVariable round-trip (all 5 types) ---

    [Fact]
    public void ToVariable_Int_RoundTrips()
    {
        var vm = new VariableViewModel { Name = "nHP", Type = VariableType.Int, IntValue = 100 };

        var v = vm.ToVariable();

        Assert.Equal("nHP", v.Name);
        Assert.Equal(VariableType.Int, v.Type);
        Assert.Equal(100, v.GetInt());
    }

    [Fact]
    public void ToVariable_Float_RoundTrips()
    {
        var vm = new VariableViewModel { Name = "fSpeed", Type = VariableType.Float, FloatValue = 1.5m };

        var v = vm.ToVariable();

        Assert.Equal(VariableType.Float, v.Type);
        Assert.Equal(1.5f, v.GetFloat(), 0.001f);
    }

    [Fact]
    public void ToVariable_String_RoundTrips()
    {
        var vm = new VariableViewModel { Name = "sTag", Type = VariableType.String, StringValue = "my_item" };

        var v = vm.ToVariable();

        Assert.Equal(VariableType.String, v.Type);
        Assert.Equal("my_item", v.GetString());
    }

    [Fact]
    public void ToVariable_Object_RoundTrips()
    {
        var vm = new VariableViewModel { Name = "oRef", Type = VariableType.Object, ObjectIdValue = 0xABCDu };

        var v = vm.ToVariable();

        Assert.Equal(VariableType.Object, v.Type);
        Assert.Equal(0xABCDu, v.GetObjectId());
    }

    [Fact]
    public void ToVariable_Location_RoundTrips()
    {
        var vm = new VariableViewModel
        {
            Name = "lHome", Type = VariableType.Location,
            LocationArea = 9, LocationPositionX = 4.25f, LocationPositionY = 5.5f, LocationPositionZ = 6.75f,
            LocationOrientationX = 1f, LocationOrientationY = 0f, LocationOrientationZ = 0f
        };

        var v = vm.ToVariable();
        var loc = v.GetLocation();

        Assert.Equal(VariableType.Location, v.Type);
        Assert.NotNull(loc);
        Assert.Equal(9u, loc!.Area);
        Assert.Equal(4.25f, loc.PositionX, 0.001f);
        Assert.Equal(6.75f, loc.PositionZ, 0.001f);
        Assert.Equal(1f, loc.OrientationX, 0.001f);
    }

    [Fact]
    public void FromVariable_AndBack_PreservesFloatData()
    {
        var original = Variable.CreateFloat("fMultiplier", 1.234f);

        var result = VariableViewModel.FromVariable(original).ToVariable();

        Assert.Equal(original.Name, result.Name);
        Assert.Equal(original.GetFloat(), result.GetFloat(), 0.001f);
    }

    // --- TypeIndex: 5-type map (0=Int..4=Location), NOT the old 3-type map ---

    [Fact]
    public void TypeIndex_MapsAll5Types()
    {
        var vm = new VariableViewModel();

        vm.TypeIndex = 0; Assert.Equal(VariableType.Int, vm.Type);
        vm.TypeIndex = 1; Assert.Equal(VariableType.Float, vm.Type);
        vm.TypeIndex = 2; Assert.Equal(VariableType.String, vm.Type);
        vm.TypeIndex = 3; Assert.Equal(VariableType.Object, vm.Type);
        vm.TypeIndex = 4; Assert.Equal(VariableType.Location, vm.Type);
    }

    [Fact]
    public void TypeIndex_Getter_ReflectsType()
    {
        Assert.Equal(0, new VariableViewModel { Type = VariableType.Int }.TypeIndex);
        Assert.Equal(3, new VariableViewModel { Type = VariableType.Object }.TypeIndex);
        Assert.Equal(4, new VariableViewModel { Type = VariableType.Location }.TypeIndex);
    }

    [Fact]
    public void TypeDisplay_ShowsCorrectStrings()
    {
        Assert.Equal("Int", new VariableViewModel { Type = VariableType.Int }.TypeDisplay);
        Assert.Equal("Float", new VariableViewModel { Type = VariableType.Float }.TypeDisplay);
        Assert.Equal("String", new VariableViewModel { Type = VariableType.String }.TypeDisplay);
        Assert.Equal("Object", new VariableViewModel { Type = VariableType.Object }.TypeDisplay);
        Assert.Equal("Location", new VariableViewModel { Type = VariableType.Location }.TypeDisplay);
    }

    [Fact]
    public void ValueDisplay_ShowsCorrectFormat()
    {
        Assert.Equal("42", new VariableViewModel { Type = VariableType.Int, IntValue = 42 }.ValueDisplay);
        Assert.Equal("hello", new VariableViewModel { Type = VariableType.String, StringValue = "hello" }.ValueDisplay);
    }

    // --- PropertyChanged plumbing ---

    [Fact]
    public void PropertyChanged_RaisedOnNameChange()
    {
        var vm = new VariableViewModel();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName != null) changed.Add(e.PropertyName); };

        vm.Name = "test";

        Assert.Contains(nameof(VariableViewModel.Name), changed);
        Assert.Contains(nameof(VariableViewModel.HasEmptyName), changed);
    }

    [Fact]
    public void TypeChange_RaisesMultiplePropertyChanged()
    {
        var vm = new VariableViewModel { Type = VariableType.Int };
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName != null) changed.Add(e.PropertyName); };

        vm.Type = VariableType.String;

        Assert.Contains(nameof(VariableViewModel.Type), changed);
        Assert.Contains(nameof(VariableViewModel.TypeDisplay), changed);
        Assert.Contains(nameof(VariableViewModel.ValueDisplay), changed);
        Assert.Contains(nameof(VariableViewModel.IsIntType), changed);
        Assert.Contains(nameof(VariableViewModel.IsStringType), changed);
    }

    // --- Validation flag fields (Relique pattern, kept) ---

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
        var vm = new VariableViewModel { HasError = true, ErrorMessage = "Duplicate name" };
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
    public void HasEmptyName_TrueWhenEmpty() => Assert.True(new VariableViewModel { Name = "" }.HasEmptyName);

    [Fact]
    public void HasEmptyName_FalseWhenSet() => Assert.False(new VariableViewModel { Name = "nCount" }.HasEmptyName);

    // --- NWN name-rule validation (new, #2293) ---

    [Theory]
    [InlineData("nCount", true)]
    [InlineData("my_var_1", true)]
    [InlineData("ABC123", true)]
    [InlineData("", false)]            // empty
    [InlineData("has space", false)]   // space not allowed
    [InlineData("bad-dash", false)]    // dash not allowed
    [InlineData("dot.name", false)]    // dot not allowed
    public void IsValidName_EnforcesCharacterRules(string name, bool expected)
    {
        Assert.Equal(expected, VariableViewModel.IsValidName(name));
    }

    [Fact]
    public void IsValidName_RejectsOver32Chars()
    {
        Assert.True(VariableViewModel.IsValidName(new string('a', 32)));
        Assert.False(VariableViewModel.IsValidName(new string('a', 33)));
    }

    // --- ToVariable with empty name still succeeds (validation is UI-layer) ---

    [Fact]
    public void ToVariable_EmptyName_StillCreatesVariable()
    {
        var v = new VariableViewModel { Name = "", Type = VariableType.Int, IntValue = 0 }.ToVariable();
        Assert.Equal("", v.Name);
    }

    // --- Raw value text is never discarded on bad input (#2293 follow-up) ---

    [Fact]
    public void BadIntText_IsKept_NotReverted()
    {
        var vm = new VariableViewModel { Type = VariableType.Int, ValueText = "5" };

        vm.ValueText = "abc"; // user typed letters into an int

        Assert.Equal("abc", vm.ValueText);     // text preserved, not undone
        Assert.False(vm.IsValueValid());
        Assert.Equal(0, vm.IntValue);          // typed accessor degrades safely
    }

    [Fact]
    public void ValidateValue_ReturnsMessage_ForBadInt()
    {
        var vm = new VariableViewModel { Type = VariableType.Int, ValueText = "abc" };

        var msg = vm.ValidateValue();

        Assert.NotNull(msg);
        Assert.Contains("whole number", msg!);
    }

    [Fact]
    public void SwitchingTypeToString_MakesBadNumberValid()
    {
        var vm = new VariableViewModel { Type = VariableType.Int, ValueText = "abc" };
        Assert.False(vm.IsValueValid());

        vm.Type = VariableType.String; // user corrects by switching type

        Assert.True(vm.IsValueValid());
        Assert.Equal("abc", vm.StringValue); // their text survives the switch
    }

    [Fact]
    public void IsValueValid_StringAlwaysValid()
    {
        Assert.True(new VariableViewModel { Type = VariableType.String, ValueText = "anything @#$" }.IsValueValid());
    }

    [Fact]
    public void BadFloatText_FlaggedButKept()
    {
        var vm = new VariableViewModel { Type = VariableType.Float, ValueText = "1.2.3" };
        Assert.False(vm.IsValueValid());
        Assert.Equal("1.2.3", vm.ValueText);
    }

    [Fact]
    public void ValueText_Change_RaisesValueDisplay()
    {
        var vm = new VariableViewModel { Type = VariableType.Int };
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName != null) changed.Add(e.PropertyName); };

        vm.ValueText = "42";

        Assert.Contains(nameof(VariableViewModel.ValueText), changed);
        Assert.Contains(nameof(VariableViewModel.ValueDisplay), changed);
    }
}
