using ItemEditor.ViewModels;
using Radoub.Formats.Uti;
using Xunit;

namespace ItemEditor.Tests.ViewModels;

public class ItemViewModelConditionalTests
{
    [Fact]
    public void ModelPart1_GetSet_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.ModelPart1 = 5;

        Assert.Equal((byte)5, uti.ModelPart1);
    }

    [Fact]
    public void ModelPart2_GetSet_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.ModelPart2 = 3;

        Assert.Equal((byte)3, uti.ModelPart2);
    }

    [Fact]
    public void ModelPart3_GetSet_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.ModelPart3 = 7;

        Assert.Equal((byte)7, uti.ModelPart3);
    }

    [Fact]
    public void Cloth1Color_GetSet_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.Cloth1Color = 10;

        Assert.Equal((byte)10, uti.Cloth1Color);
    }

    [Fact]
    public void Cloth2Color_GetSet_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.Cloth2Color = 20;

        Assert.Equal((byte)20, uti.Cloth2Color);
    }

    [Fact]
    public void Leather1Color_GetSet_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.Leather1Color = 30;

        Assert.Equal((byte)30, uti.Leather1Color);
    }

    [Fact]
    public void Leather2Color_GetSet_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.Leather2Color = 40;

        Assert.Equal((byte)40, uti.Leather2Color);
    }

    [Fact]
    public void Metal1Color_GetSet_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.Metal1Color = 50;

        Assert.Equal((byte)50, uti.Metal1Color);
    }

    [Fact]
    public void Metal2Color_GetSet_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.Metal2Color = 60;

        Assert.Equal((byte)60, uti.Metal2Color);
    }

    [Fact]
    public void GetArmorPart_ReturnsValue()
    {
        var uti = new UtiFile();
        uti.ArmorParts["Torso"] = 5;
        var vm = new ItemViewModel(uti);

        Assert.Equal((byte)5, vm.GetArmorPart("Torso"));
    }

    [Fact]
    public void GetArmorPart_MissingKey_ReturnsZero()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        Assert.Equal((byte)0, vm.GetArmorPart("Torso"));
    }

    [Fact]
    public void SetArmorPart_UpdatesUtiFile()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.SetArmorPart("Torso", 3);

        Assert.Equal((byte)3, uti.ArmorParts["Torso"]);
    }

    [Fact]
    public void SetArmorPart_RaisesPropertyChanged()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);
        bool changed = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "ArmorPart_Torso") changed = true;
        };

        vm.SetArmorPart("Torso", 3);

        Assert.True(changed);
    }
}
