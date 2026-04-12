using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Utp;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class UtpSearchProviderTests
{
    private static UtpFile CreateTestUtp()
    {
        return new UtpFile
        {
            LocName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Treasure Chest"
            }},
            Description = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "A sturdy wooden chest."
            }},
            Tag = "TREASURE_CHEST",
            TemplateResRef = "plc_chest001",
            Comment = "Loot container for quest reward",
            Conversation = "chest_conv",
            HasInventory = true,
            ItemList = new List<PlaceableItem>
            {
                new PlaceableItem { InventoryRes = "poisoned_apple" },
                new PlaceableItem { InventoryRes = "nw_it_gem001" },
                new PlaceableItem { InventoryRes = "quest_amulet" }
            }
        };
    }

    private static GffFile UtpToGff(UtpFile utp)
    {
        var bytes = UtpWriter.Write(utp);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Search_FindsInventoryResRef()
    {
        var provider = new UtpSearchProvider();
        var gff = UtpToGff(CreateTestUtp());
        var criteria = new SearchCriteria { Pattern = "poisoned_apple" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "InventoryRes" &&
            m.MatchedText == "poisoned_apple" &&
            (m.Location as string)!.Contains("Item 0"));
    }

    [Fact]
    public void Search_FindsMultipleInventoryItems()
    {
        var provider = new UtpSearchProvider();
        var gff = UtpToGff(CreateTestUtp());
        var criteria = new SearchCriteria { Pattern = "nw_it_gem001|quest_amulet", IsRegex = true };

        var matches = provider.Search(gff, criteria);

        var inventoryMatches = matches.Where(m => m.Field.Name == "InventoryRes").ToList();
        Assert.Equal(2, inventoryMatches.Count);
    }

    [Fact]
    public void Search_InventoryLocationIncludesItemIndex()
    {
        var provider = new UtpSearchProvider();
        var gff = UtpToGff(CreateTestUtp());
        var criteria = new SearchCriteria { Pattern = "quest_amulet" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches, m => m.Field.Name == "InventoryRes");
        Assert.Contains("Item 2", match.Location as string);
    }

    [Fact]
    public void Search_EmptyInventory_NoInventoryMatches()
    {
        var utp = CreateTestUtp();
        utp.ItemList.Clear();
        var provider = new UtpSearchProvider();
        var gff = UtpToGff(utp);
        var criteria = new SearchCriteria { Pattern = "poisoned_apple" };

        var matches = provider.Search(gff, criteria);

        Assert.DoesNotContain(matches, m => m.Field.Name == "InventoryRes");
    }

    [Fact]
    public void Search_InventoryResFieldIsNotReplaceable()
    {
        var provider = new UtpSearchProvider();
        var gff = UtpToGff(CreateTestUtp());
        var criteria = new SearchCriteria { Pattern = "poisoned_apple" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches, m => m.Field.Name == "InventoryRes");
        Assert.False(match.Field.IsReplaceable);
    }

    [Fact]
    public void Search_FindsLocName()
    {
        var provider = new UtpSearchProvider();
        var gff = UtpToGff(CreateTestUtp());
        var criteria = new SearchCriteria { Pattern = "Treasure Chest" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Name" && m.MatchedText == "Treasure Chest");
    }

    [Fact]
    public void Search_FindsTag()
    {
        var provider = new UtpSearchProvider();
        var gff = UtpToGff(CreateTestUtp());
        var criteria = new SearchCriteria { Pattern = "TREASURE_CHEST" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Tag");
    }

    [Fact]
    public void Search_WithStrRefResolver_MatchesTlkText()
    {
        var utp = CreateTestUtp();
        utp.LocName = new CExoLocString { StrRef = 5000 }; // No inline text, only StrRef
        var provider = new UtpSearchProvider();
        var gff = UtpToGff(utp);
        var criteria = new SearchCriteria
        {
            Pattern = "Magic Chest",
            SearchStrRefs = true,
            TlkResolver = strRef => strRef == 5000 ? "Magic Chest of Wonders" : null
        };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "Name" &&
            m.MatchedText == "Magic Chest" &&
            m.LanguageId == null); // TLK-resolved, not a specific language
    }

    [Fact]
    public void Search_WithoutStrRefFlag_IgnoresTlk()
    {
        var utp = CreateTestUtp();
        utp.LocName = new CExoLocString { StrRef = 5000 }; // No inline text
        var provider = new UtpSearchProvider();
        var gff = UtpToGff(utp);
        var criteria = new SearchCriteria
        {
            Pattern = "Magic Chest",
            SearchStrRefs = false,
            TlkResolver = strRef => strRef == 5000 ? "Magic Chest of Wonders" : null
        };

        var matches = provider.Search(gff, criteria);

        Assert.DoesNotContain(matches, m => m.Field.Name == "Name");
    }

    [Fact]
    public void Search_FieldTypeFilter_OnlySearchesInventory()
    {
        var provider = new UtpSearchProvider();
        var utp = CreateTestUtp();
        utp.TemplateResRef = "poisoned_apple";  // Same as an inventory item
        var gff = UtpToGff(utp);
        var criteria = new SearchCriteria
        {
            Pattern = "poisoned_apple",
            FieldTypeFilter = new[] { SearchFieldType.ResRef }
        };

        var matches = provider.Search(gff, criteria);

        // Should find in both ResRef and InventoryRes (both are ResRef type)
        Assert.Contains(matches, m => m.Field.Name == "InventoryRes");
        Assert.Contains(matches, m => m.Field.Name == "ResRef");
    }
}
