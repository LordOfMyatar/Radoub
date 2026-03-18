using ItemEditor.ViewModels;
using Radoub.Formats.Gff;
using Radoub.Formats.Uti;
using Xunit;

namespace ItemEditor.Tests.ViewModels;

public class ItemViewModelTests
{
    [Fact]
    public void Constructor_LoadsAllBasicProperties()
    {
        var uti = CreateTestItem();

        var vm = new ItemViewModel(uti);

        Assert.Equal("Magic Sword", vm.Name);
        Assert.Equal("SWORD_TAG", vm.Tag);
        Assert.Equal("test_sword", vm.ResRef);
        Assert.Equal(4, vm.BaseItem);
        Assert.Equal(500u, vm.Cost);
        Assert.Equal(100u, vm.AddCost);
    }

    [Fact]
    public void SetName_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.Name = "New Name";

        Assert.Equal("New Name", uti.LocalizedName.GetDefault());
    }

    [Fact]
    public void SetTag_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.Tag = "NEW_TAG";

        Assert.Equal("NEW_TAG", uti.Tag);
    }

    [Fact]
    public void SetResRef_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.ResRef = "new_resref";

        Assert.Equal("new_resref", uti.TemplateResRef);
    }

    [Fact]
    public void SetBaseItem_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.BaseItem = 16; // Armor

        Assert.Equal(16, uti.BaseItem);
    }

    [Fact]
    public void SetCost_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.Cost = 999;

        Assert.Equal(999u, uti.Cost);
    }

    [Fact]
    public void SetAddCost_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.AddCost = 50;

        Assert.Equal(50u, uti.AddCost);
    }

    [Fact]
    public void PropertyChanged_RaisedOnNameChange()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);
        string? changedProperty = null;
        vm.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        vm.Name = "Changed";

        Assert.Equal(nameof(ItemViewModel.Name), changedProperty);
    }

    [Fact]
    public void PropertyChanged_NotRaisedWhenSameValue()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);
        bool changed = false;
        vm.PropertyChanged += (_, _) => changed = true;

        vm.Tag = uti.Tag; // Same value

        Assert.False(changed);
    }

    [Fact]
    public void SetStackSize_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.StackSize = 10;

        Assert.Equal((ushort)10, uti.StackSize);
    }

    [Fact]
    public void SetCharges_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.Charges = 5;

        Assert.Equal((byte)5, uti.Charges);
    }

    [Fact]
    public void SetPlot_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.Plot = true;

        Assert.True(uti.Plot);
    }

    [Fact]
    public void SetCursed_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.Cursed = true;

        Assert.True(uti.Cursed);
    }

    [Fact]
    public void SetStolen_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.Stolen = true;

        Assert.True(uti.Stolen);
    }

    [Fact]
    public void SetComment_UpdatesUtiFile()
    {
        var uti = CreateTestItem();
        var vm = new ItemViewModel(uti);

        vm.Comment = "Test comment";

        Assert.Equal("Test comment", uti.Comment);
    }

    [Fact]
    public void Constructor_WithNullLocalizedName_ReturnsEmpty()
    {
        var uti = new UtiFile();

        var vm = new ItemViewModel(uti);

        Assert.Equal(string.Empty, vm.Name);
    }

    [Fact]
    public void SetName_WhenLocalizedNameWasNull_CreatesLocString()
    {
        var uti = new UtiFile();
        var vm = new ItemViewModel(uti);

        vm.Name = "New Item";

        Assert.False(uti.LocalizedName.IsEmpty);
        Assert.Equal("New Item", uti.LocalizedName.GetDefault());
    }

    #region Test Helpers

    private static UtiFile CreateTestItem()
    {
        var uti = new UtiFile
        {
            TemplateResRef = "test_sword",
            Tag = "SWORD_TAG",
            BaseItem = 4,
            StackSize = 1,
            Charges = 0,
            Cost = 500,
            AddCost = 100,
            Plot = false,
            Cursed = false,
            Stolen = false,
            Comment = ""
        };
        uti.LocalizedName.LocalizedStrings[0] = "Magic Sword";
        return uti;
    }

    #endregion
}
