using System.ComponentModel;
using Radoub.Formats.Utp;
using PlaceableEditor.ViewModels;

namespace PlaceableEditor.Tests.ViewModels;

/// <summary>
/// Behavior tests for the UTP binding facade: field bindings write through to the model,
/// PropertyChanged fires, and the Static/Plot derived-enablement logic (design §5.1) holds.
/// </summary>
public class PlaceableViewModelTests
{
    private static UtpFile MakeUtp()
    {
        var utp = new UtpFile { Tag = "TG_CHEST", TemplateResRef = "chest1" };
        utp.LocName.SetString(0, "Iron Chest");
        utp.HP = 15;
        utp.Hardness = 5;
        return utp;
    }

    [Fact]
    public void Name_BindsToLocName()
    {
        var vm = new PlaceableViewModel(MakeUtp());
        Assert.Equal("Iron Chest", vm.Name);
    }

    [Fact]
    public void SettingName_WritesThroughToModel()
    {
        var utp = MakeUtp();
        var vm = new PlaceableViewModel(utp);

        vm.Name = "Oak Chest";

        Assert.Equal("Oak Chest", utp.LocName.GetDefault());
    }

    [Fact]
    public void SettingTag_RaisesPropertyChanged()
    {
        var vm = new PlaceableViewModel(MakeUtp());
        string? changed = null;
        vm.PropertyChanged += (_, e) => changed = e.PropertyName;

        vm.Tag = "TG_NEW";

        Assert.Equal(nameof(vm.Tag), changed);
        Assert.Equal("TG_NEW", vm.Utp.Tag);
    }

    [Fact]
    public void Hp_WritesThroughToModel()
    {
        var utp = MakeUtp();
        var vm = new PlaceableViewModel(utp);

        vm.HP = 40;

        Assert.Equal((short)40, utp.HP);
    }

    [Fact]
    public void SettingSameValue_DoesNotRaisePropertyChanged()
    {
        var vm = new PlaceableViewModel(MakeUtp());
        int count = 0;
        vm.PropertyChanged += (_, _) => count++;

        vm.Tag = "TG_CHEST"; // unchanged

        Assert.Equal(0, count);
    }

    [Fact]
    public void Static_DisablesCombatFields()
    {
        var vm = new PlaceableViewModel(MakeUtp());

        vm.Static = true;

        Assert.False(vm.IsCombatEnabled); // HP/Hardness/saves disabled when static
    }

    [Fact]
    public void NotStatic_EnablesCombatFields()
    {
        var vm = new PlaceableViewModel(MakeUtp());
        vm.Static = false;
        Assert.True(vm.IsCombatEnabled);
    }

    [Fact]
    public void Plot_DisablesDamageFields_ButLeavesCombatEnabled()
    {
        var vm = new PlaceableViewModel(MakeUtp());

        vm.Plot = true;

        Assert.False(vm.IsDamageEnabled); // plot objects cannot be damaged
        Assert.True(vm.IsCombatEnabled);  // saves still meaningful
    }

    [Fact]
    public void Static_AlsoDisablesDamageFields()
    {
        var vm = new PlaceableViewModel(MakeUtp());
        vm.Static = true;
        Assert.False(vm.IsDamageEnabled);
    }

    [Fact]
    public void SettingStatic_RaisesIsCombatEnabledChange()
    {
        var vm = new PlaceableViewModel(MakeUtp());
        var props = new System.Collections.Generic.List<string?>();
        vm.PropertyChanged += (_, e) => props.Add(e.PropertyName);

        vm.Static = true;

        Assert.Contains(nameof(vm.IsCombatEnabled), props);
        Assert.Contains(nameof(vm.IsDamageEnabled), props);
    }

    [Fact]
    public void SettingPlot_RaisesIsDamageEnabledChange()
    {
        var vm = new PlaceableViewModel(MakeUtp());
        var props = new System.Collections.Generic.List<string?>();
        vm.PropertyChanged += (_, e) => props.Add(e.PropertyName);

        vm.Plot = true;

        Assert.Contains(nameof(vm.IsDamageEnabled), props);
    }

    [Fact]
    public void HasInventory_TogglesAndWritesThrough()
    {
        var utp = MakeUtp();
        var vm = new PlaceableViewModel(utp);

        vm.HasInventory = true;

        Assert.True(utp.HasInventory);
    }

    [Fact]
    public void NoInterrupt_IsInverseOfInterruptable()
    {
        var utp = new UtpFile { Interruptable = true };
        var vm = new PlaceableViewModel(utp);

        Assert.False(vm.NoInterrupt);

        vm.NoInterrupt = true;

        Assert.False(utp.Interruptable);
        Assert.True(vm.NoInterrupt);
    }

    [Fact]
    public void Conversation_BindsToModel()
    {
        var utp = new UtpFile { Conversation = "lamp_talk" };
        var vm = new PlaceableViewModel(utp);
        Assert.Equal("lamp_talk", vm.Conversation);

        vm.Conversation = "new_dlg";
        Assert.Equal("new_dlg", utp.Conversation);
    }
}
