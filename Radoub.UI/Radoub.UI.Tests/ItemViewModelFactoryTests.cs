using Radoub.Formats.Gff;
using Radoub.Formats.Uti;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.ViewModels;
using Xunit;

namespace Radoub.UI.Tests;

public class ItemViewModelFactoryTests
{
    [Fact]
    public void Create_UsesLocalizedNameWhenAvailable()
    {
        // Arrange
        var item = new UtiFile
        {
            LocalizedName = new CExoLocString()
        };
        item.LocalizedName.SetString(0, "My Sword");

        var mockGameData = new MockGameDataService(includeSampleData: false);
        var factory = new ItemViewModelFactory(mockGameData);

        // Act
        var vm = factory.Create(item);

        // Assert
        Assert.Equal("My Sword", vm.Name);
    }

    [Fact]
    public void Create_FallsBackToResRefWhenNoLocalizedName()
    {
        // Arrange
        var item = new UtiFile
        {
            TemplateResRef = "fallback_sword"
        };

        var mockGameData = new MockGameDataService(includeSampleData: false);
        var factory = new ItemViewModelFactory(mockGameData);

        // Act
        var vm = factory.Create(item);

        // Assert
        Assert.Equal("fallback_sword", vm.Name);
    }

    [Fact]
    public void Create_ResolvesBaseItemNameFrom2DA()
    {
        // Arrange
        var item = new UtiFile
        {
            BaseItem = 1,
            TemplateResRef = "test"
        };

        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 1, "Name", "12345");
        mockGameData.SetTlkString(12345, "Longsword");
        var factory = new ItemViewModelFactory(mockGameData);

        // Act
        var vm = factory.Create(item);

        // Assert
        Assert.Equal("Longsword", vm.BaseItemName);
    }

    [Fact]
    public void Create_FallsBackToBaseItemLabel()
    {
        // Arrange
        var item = new UtiFile
        {
            BaseItem = 42,
            TemplateResRef = "test"
        };

        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("baseitems", 42, "label", "CustomItem");
        var factory = new ItemViewModelFactory(mockGameData);

        // Act
        var vm = factory.Create(item);

        // Assert
        Assert.Equal("CustomItem", vm.BaseItemName);
    }

    [Fact]
    public void Create_FallsBackToTypeNumber()
    {
        // Arrange
        var item = new UtiFile
        {
            BaseItem = 999,
            TemplateResRef = "test"
        };

        var mockGameData = new MockGameDataService(includeSampleData: false);
        var factory = new ItemViewModelFactory(mockGameData);

        // Act
        var vm = factory.Create(item);

        // Assert
        Assert.Equal("Type 999", vm.BaseItemName);
    }

    [Fact]
    public void Create_ResolvesMultipleProperties()
    {
        // Arrange
        var item = new UtiFile
        {
            TemplateResRef = "test",
            Properties = new List<ItemProperty>
            {
                new ItemProperty { PropertyName = 0 },
                new ItemProperty { PropertyName = 1 }
            }
        };

        var mockGameData = new MockGameDataService(includeSampleData: false);
        mockGameData.Set2DAValue("itempropdef", 0, "GameStrRef", "100");
        mockGameData.Set2DAValue("itempropdef", 1, "GameStrRef", "101");
        mockGameData.SetTlkString(100, "Enhancement Bonus");
        mockGameData.SetTlkString(101, "Damage Bonus");
        var factory = new ItemViewModelFactory(mockGameData);

        // Act
        var vm = factory.Create(item);

        // Assert
        Assert.Contains("Enhancement Bonus", vm.PropertiesDisplay);
        Assert.Contains("Damage Bonus", vm.PropertiesDisplay);
        Assert.Contains("; ", vm.PropertiesDisplay);
    }

    [Fact]
    public void Create_HandlesEmptyProperties()
    {
        // Arrange
        var item = new UtiFile
        {
            TemplateResRef = "test",
            Properties = new List<ItemProperty>()
        };

        var mockGameData = new MockGameDataService(includeSampleData: false);
        var factory = new ItemViewModelFactory(mockGameData);

        // Act
        var vm = factory.Create(item);

        // Assert
        Assert.Equal(string.Empty, vm.PropertiesDisplay);
    }

    [Fact]
    public void CreateBatch_CreatesMultipleViewModels()
    {
        // Arrange
        var items = new List<UtiFile>
        {
            new UtiFile { TemplateResRef = "item1" },
            new UtiFile { TemplateResRef = "item2" },
            new UtiFile { TemplateResRef = "item3" }
        };

        var mockGameData = new MockGameDataService(includeSampleData: false);
        var factory = new ItemViewModelFactory(mockGameData);

        // Act
        var viewModels = factory.Create(items).ToList();

        // Assert
        Assert.Equal(3, viewModels.Count);
        Assert.Equal("item1", viewModels[0].ResRef);
        Assert.Equal("item2", viewModels[1].ResRef);
        Assert.Equal("item3", viewModels[2].ResRef);
    }
}
