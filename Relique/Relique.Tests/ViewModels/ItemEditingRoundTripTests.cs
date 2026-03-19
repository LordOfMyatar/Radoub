using ItemEditor.ViewModels;
using Radoub.Formats.Gff;
using Radoub.Formats.Uti;
using Xunit;

namespace ItemEditor.Tests.ViewModels;

/// <summary>
/// Round-trip tests: create UtiFile → edit via ViewModel → write → read back → verify.
/// Ensures that edits through the ViewModel produce valid UTI files.
/// </summary>
public class ItemEditingRoundTripTests
{
    [Fact]
    public void RoundTrip_EditName_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Name = "Enchanted Blade";

        var result = WriteAndReadBack(uti);
        Assert.Equal("Enchanted Blade", result.LocalizedName.GetDefault());
    }

    [Fact]
    public void RoundTrip_EditTag_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Tag = "NEW_ITEM_TAG";

        var result = WriteAndReadBack(uti);
        Assert.Equal("NEW_ITEM_TAG", result.Tag);
    }

    [Fact]
    public void RoundTrip_EditResRef_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.ResRef = "new_resref";

        var result = WriteAndReadBack(uti);
        Assert.Equal("new_resref", result.TemplateResRef);
    }

    [Fact]
    public void RoundTrip_EditBaseItem_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.BaseItem = 16; // Armor

        var result = WriteAndReadBack(uti);
        Assert.Equal(16, result.BaseItem);
    }

    [Fact]
    public void RoundTrip_EditCost_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Cost = 12345;
        vm.AddCost = 678;

        var result = WriteAndReadBack(uti);
        Assert.Equal(12345u, result.Cost);
        Assert.Equal(678u, result.AddCost);
    }

    [Fact]
    public void RoundTrip_EditFlags_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Plot = true;
        vm.Cursed = true;
        vm.Stolen = true;

        var result = WriteAndReadBack(uti);
        Assert.True(result.Plot);
        Assert.True(result.Cursed);
        Assert.True(result.Stolen);
    }

    [Fact]
    public void RoundTrip_EditStackSize_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.StackSize = 99;

        var result = WriteAndReadBack(uti);
        Assert.Equal((ushort)99, result.StackSize);
    }

    [Fact]
    public void RoundTrip_EditCharges_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Charges = 50;

        var result = WriteAndReadBack(uti);
        Assert.Equal((byte)50, result.Charges);
    }

    [Fact]
    public void RoundTrip_EditComment_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Comment = "This is a test comment";

        var result = WriteAndReadBack(uti);
        Assert.Equal("This is a test comment", result.Comment);
    }

    [Fact]
    public void RoundTrip_EditPaletteID_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.PaletteID = 3;

        var result = WriteAndReadBack(uti);
        Assert.Equal((byte)3, result.PaletteID);
    }

    [Fact]
    public void RoundTrip_EditModelParts_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.ModelPart1 = 5;
        vm.ModelPart2 = 3;
        vm.ModelPart3 = 7;

        var result = WriteAndReadBack(uti);
        Assert.Equal((byte)5, result.ModelPart1);
        Assert.Equal((byte)3, result.ModelPart2);
        Assert.Equal((byte)7, result.ModelPart3);
    }

    [Fact]
    public void RoundTrip_EditColors_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Cloth1Color = 10;
        vm.Cloth2Color = 20;
        vm.Leather1Color = 30;
        vm.Leather2Color = 40;
        vm.Metal1Color = 50;
        vm.Metal2Color = 60;

        var result = WriteAndReadBack(uti);
        Assert.Equal((byte)10, result.Cloth1Color);
        Assert.Equal((byte)20, result.Cloth2Color);
        Assert.Equal((byte)30, result.Leather1Color);
        Assert.Equal((byte)40, result.Leather2Color);
        Assert.Equal((byte)50, result.Metal1Color);
        Assert.Equal((byte)60, result.Metal2Color);
    }

    [Fact]
    public void RoundTrip_EditArmorParts_Preserved()
    {
        var uti = CreateBaseItem();
        uti.BaseItem = 16; // Armor
        var vm = new ItemViewModel(uti);

        vm.SetArmorPart("Torso", 5);
        vm.SetArmorPart("Belt", 2);
        vm.SetArmorPart("LBicep", 3);

        var result = WriteAndReadBack(uti);
        Assert.Equal((byte)5, result.ArmorParts["Torso"]);
        Assert.Equal((byte)2, result.ArmorParts["Belt"]);
        Assert.Equal((byte)3, result.ArmorParts["LBicep"]);
    }

    [Fact]
    public void RoundTrip_EditIdentifiedAndDropableFlags_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Identified = true;
        vm.Dropable = false;
        vm.Plot = true;
        vm.Cursed = true;

        var result = WriteAndReadBack(uti);
        Assert.True(result.Identified);
        Assert.False(result.Dropable);
        Assert.True(result.Plot);
        Assert.True(result.Cursed);
    }

    [Fact]
    public void RoundTrip_MultipleEdits_AllPreserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        // Edit everything at once
        vm.Name = "Dragon Slayer";
        vm.Tag = "DRAGON_SLAYER";
        vm.ResRef = "dragon_slayer";
        vm.BaseItem = 1; // Longsword
        vm.Cost = 50000;
        vm.AddCost = 10000;
        vm.StackSize = 1;
        vm.Charges = 0;
        vm.Plot = true;
        vm.Cursed = false;
        vm.Stolen = false;
        vm.Comment = "Legendary weapon";
        vm.PaletteID = 2;
        vm.ModelPart1 = 10;

        var result = WriteAndReadBack(uti);
        Assert.Equal("Dragon Slayer", result.LocalizedName.GetDefault());
        Assert.Equal("DRAGON_SLAYER", result.Tag);
        Assert.Equal("dragon_slayer", result.TemplateResRef);
        Assert.Equal(1, result.BaseItem);
        Assert.Equal(50000u, result.Cost);
        Assert.Equal(10000u, result.AddCost);
        Assert.Equal((ushort)1, result.StackSize);
        Assert.Equal((byte)0, result.Charges);
        Assert.True(result.Plot);
        Assert.False(result.Cursed);
        Assert.False(result.Stolen);
        Assert.Equal("Legendary weapon", result.Comment);
        Assert.Equal((byte)2, result.PaletteID);
        Assert.Equal((byte)10, result.ModelPart1);
    }

    [Fact]
    public void RoundTrip_EditIdentified_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Identified = true;

        var result = WriteAndReadBack(uti);
        Assert.True(result.Identified);
    }

    [Fact]
    public void RoundTrip_EditDropable_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Dropable = false;

        var result = WriteAndReadBack(uti);
        Assert.False(result.Dropable);
    }

    [Fact]
    public void RoundTrip_DropableDefaultTrue_Preserved()
    {
        var uti = CreateBaseItem();
        // Dropable defaults to true — verify it round-trips
        Assert.True(uti.Dropable);

        var result = WriteAndReadBack(uti);
        Assert.True(result.Dropable);
    }

    [Fact]
    public void RoundTrip_ClearName_PreservesEmptyState()
    {
        var uti = CreateBaseItem();
        uti.LocalizedName.LocalizedStrings[0] = "Original Name";
        var vm = new ItemViewModel(uti);

        vm.Name = "";

        var result = WriteAndReadBack(uti);
        Assert.Equal("", result.LocalizedName.GetDefault());
    }

    [Fact]
    public void RoundTrip_EditDescription_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Description = "A mysterious blade shrouded in darkness.";

        var result = WriteAndReadBack(uti);
        Assert.Equal("A mysterious blade shrouded in darkness.", result.Description.GetDefault());
    }

    [Fact]
    public void RoundTrip_EditDescIdentified_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.DescIdentified = "The legendary Sword of Shadows, forged in the Underdark.";

        var result = WriteAndReadBack(uti);
        Assert.Equal("The legendary Sword of Shadows, forged in the Underdark.", result.DescIdentified.GetDefault());
    }

    [Fact]
    public void RoundTrip_BothDescriptions_Preserved()
    {
        var uti = CreateBaseItem();
        var vm = new ItemViewModel(uti);

        vm.Description = "Unidentified: glowing blade";
        vm.DescIdentified = "Identified: Moonblade +3";

        var result = WriteAndReadBack(uti);
        Assert.Equal("Unidentified: glowing blade", result.Description.GetDefault());
        Assert.Equal("Identified: Moonblade +3", result.DescIdentified.GetDefault());
    }

    [Fact]
    public void RoundTrip_VarTable_IntVariable_Preserved()
    {
        var uti = CreateBaseItem();
        uti.VarTable.Add(Variable.CreateInt("nQuestState", 42));

        var result = WriteAndReadBack(uti);

        Assert.Single(result.VarTable);
        Assert.Equal("nQuestState", result.VarTable[0].Name);
        Assert.Equal(VariableType.Int, result.VarTable[0].Type);
        Assert.Equal(42, result.VarTable[0].GetInt());
    }

    [Fact]
    public void RoundTrip_VarTable_MultipleVariables_Preserved()
    {
        var uti = CreateBaseItem();
        uti.VarTable.Add(Variable.CreateInt("nCount", 10));
        uti.VarTable.Add(Variable.CreateFloat("fDamage", 2.5f));
        uti.VarTable.Add(Variable.CreateString("sOwner", "Drizzt"));

        var result = WriteAndReadBack(uti);

        Assert.Equal(3, result.VarTable.Count);
        Assert.Equal("nCount", result.VarTable[0].Name);
        Assert.Equal(10, result.VarTable[0].GetInt());
        Assert.Equal("fDamage", result.VarTable[1].Name);
        Assert.Equal(2.5f, result.VarTable[1].GetFloat(), 0.001f);
        Assert.Equal("sOwner", result.VarTable[2].Name);
        Assert.Equal("Drizzt", result.VarTable[2].GetString());
    }

    [Fact]
    public void RoundTrip_EmptyVarTable_Preserved()
    {
        var uti = CreateBaseItem();
        // VarTable starts empty

        var result = WriteAndReadBack(uti);

        Assert.Empty(result.VarTable);
    }

    #region Helpers

    private static UtiFile CreateBaseItem()
    {
        var uti = new UtiFile
        {
            TemplateResRef = "test_item",
            Tag = "TEST_TAG",
            BaseItem = 0,
            Cost = 100,
            AddCost = 0,
            StackSize = 1,
            Charges = 0,
        };
        uti.LocalizedName.LocalizedStrings[0] = "Test Item";
        return uti;
    }

    private static UtiFile WriteAndReadBack(UtiFile uti)
    {
        var bytes = UtiWriter.Write(uti);
        return UtiReader.Read(bytes);
    }

    #endregion
}
