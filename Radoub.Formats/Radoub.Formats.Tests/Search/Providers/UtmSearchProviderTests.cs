using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Utm;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class UtmSearchProviderTests
{
    private static UtmFile CreateTestUtm()
    {
        return new UtmFile
        {
            LocName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Louis Romain's Emporium"
            }},
            Tag = "LOUIS_STORE",
            ResRef = "louis_store",
            Comment = "Main merchant for western district",
            OnOpenStore = "gc_open_store",
            OnStoreClosed = "gc_close_store",
            VarTable = new List<Variable>
            {
                new Variable { Name = "nDiscount", Type = VariableType.Int, Value = 10 },
                new Variable { Name = "sGreeting", Type = VariableType.String, Value = "Welcome to the emporium" }
            }
        };
    }

    private static GffFile UtmToGff(UtmFile utm)
    {
        var bytes = UtmWriter.Write(utm);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Search_FindsLocName()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Name" && m.MatchedText == "Louis Romain");
    }

    [Fact]
    public void Search_FindsTag()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "LOUIS_STORE" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Tag");
    }

    [Fact]
    public void Search_FindsResRef()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "louis_store" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "ResRef");
    }

    [Fact]
    public void Search_FindsComment()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "western district" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Comment");
    }

    [Fact]
    public void Search_FindsOnOpenStore()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "gc_open_store" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "OnOpenStore");
    }

    [Fact]
    public void Search_FindsOnStoreClosed()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "gc_close_store" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "OnStoreClosed");
    }

    [Fact]
    public void Search_FindsVarTableName()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "nDiscount" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Local Variables");
    }

    [Fact]
    public void Search_FindsVarTableStringValue()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "emporium" };

        var matches = provider.Search(gff, criteria);

        // Should find in both LocName and VarTable string value
        Assert.Contains(matches, m => m.Field.Name == "Local Variables");
    }

    [Fact]
    public void Search_ScriptFilter()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria
        {
            Pattern = "gc_",
            FieldTypeFilter = new[] { SearchFieldType.Script }
        };

        var matches = provider.Search(gff, criteria);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.Equal(SearchFieldType.Script, m.Field.FieldType));
    }

    [Fact]
    public void Search_LocationIsFieldName()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "gc_open_store" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        Assert.Equal("OnOpenStore", match.Location as string);
    }

    [Fact]
    public void FileType_IsUtm()
    {
        var provider = new UtmSearchProvider();
        Assert.Equal(Radoub.Formats.Common.ResourceTypes.Utm, provider.FileType);
    }

    [Fact]
    public void Extensions_ContainsUtm()
    {
        var provider = new UtmSearchProvider();
        Assert.Contains(".utm", provider.Extensions);
    }

    #region Inventory ResRef Search

    private static UtmFile CreateTestUtmWithInventory()
    {
        var utm = CreateTestUtm();
        utm.StoreList = new List<StorePanel>
        {
            new StorePanel
            {
                PanelId = StorePanels.Weapons,
                Items = new List<StoreItem>
                {
                    new StoreItem { InventoryRes = "nw_wswls001" },
                    new StoreItem { InventoryRes = "cep_halberd" }
                }
            },
            new StorePanel
            {
                PanelId = StorePanels.Armor,
                Items = new List<StoreItem>
                {
                    new StoreItem { InventoryRes = "nw_aarcl001" }
                }
            }
        };
        return utm;
    }

    [Fact]
    public void Search_FindsInventoryResRef()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtmWithInventory());
        var criteria = new SearchCriteria { Pattern = "nw_wswls001" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "InventoryRes" && m.MatchedText == "nw_wswls001");
    }

    [Fact]
    public void Search_FindsInventoryResRef_PartialMatch()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtmWithInventory());
        var criteria = new SearchCriteria { Pattern = "cep_" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "InventoryRes" && m.MatchedText == "cep_");
    }

    [Fact]
    public void Search_InventoryResRef_LocationIncludesPanelName()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtmWithInventory());
        var criteria = new SearchCriteria { Pattern = "nw_wswls001" };

        var matches = provider.Search(gff, criteria);

        var match = matches.First(m => m.Field.Name == "InventoryRes" && m.MatchedText == "nw_wswls001");
        var location = match.Location as string;
        Assert.NotNull(location);
        Assert.Contains("Weapons", location);
        Assert.Contains("InventoryRes", location);
    }

    [Fact]
    public void Search_InventoryResRef_FindsAcrossPanels()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtmWithInventory());
        var criteria = new SearchCriteria { Pattern = "nw_" };

        var matches = provider.Search(gff, criteria);

        var inventoryMatches = matches.Where(m => m.Field.Name == "InventoryRes").ToList();
        Assert.Equal(2, inventoryMatches.Count);
    }

    [Fact]
    public void Replace_InventoryResRef_UpdatesValue()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtmWithInventory());
        var criteria = new SearchCriteria { Pattern = "cep_halberd" };

        var matches = provider.Search(gff, criteria);
        var inventoryMatch = matches.First(m => m.Field.Name == "InventoryRes");

        var replaceOps = new List<ReplaceOperation>
        {
            new ReplaceOperation { Match = inventoryMatch, ReplacementText = "new_halberd" }
        };

        var results = provider.Replace(gff, replaceOps);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal("cep_halberd", results[0].OldValue);
        Assert.Equal("new_halberd", results[0].NewValue);

        // Verify the GFF was actually modified — re-search should find the new value
        var verifyMatches = provider.Search(gff, new SearchCriteria { Pattern = "new_halberd" });
        Assert.Contains(verifyMatches, m => m.Field.Name == "InventoryRes");
    }

    [Fact]
    public void Replace_InventoryResRef_TruncatesAt16Chars()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtmWithInventory());
        var criteria = new SearchCriteria { Pattern = "cep_halberd" };

        var matches = provider.Search(gff, criteria);
        var inventoryMatch = matches.First(m => m.Field.Name == "InventoryRes");

        var replaceOps = new List<ReplaceOperation>
        {
            new ReplaceOperation { Match = inventoryMatch, ReplacementText = "this_is_way_too_long_resref" }
        };

        var results = provider.Replace(gff, replaceOps);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.NotNull(results[0].Warning);
        Assert.Contains("truncated", results[0].Warning);
    }

    #endregion

    #region Inventory Item Name Search

    [Fact]
    public void Search_FindsInventoryItemByResolvedName()
    {
        // Simulate: nw_wswls001 resolves to "Longsword", cep_halberd resolves to "Halberd"
        var resolver = new Func<string, string?>(resRef => resRef switch
        {
            "nw_wswls001" => "Longsword",
            "cep_halberd" => "Halberd",
            "nw_aarcl001" => "Chainmail",
            _ => null
        });

        var provider = new UtmSearchProvider(resolver);
        var gff = UtmToGff(CreateTestUtmWithInventory());
        var criteria = new SearchCriteria { Pattern = "Longsword" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "InventoryItemName" && m.MatchedText == "Longsword");
    }

    [Fact]
    public void Search_FindsInventoryItemByPartialName()
    {
        var resolver = new Func<string, string?>(resRef => resRef switch
        {
            "nw_wswls001" => "Longsword +1",
            "cep_halberd" => "Halberd of Flame",
            "nw_aarcl001" => "Chainmail",
            _ => null
        });

        var provider = new UtmSearchProvider(resolver);
        var gff = UtmToGff(CreateTestUtmWithInventory());
        var criteria = new SearchCriteria { Pattern = "Halberd" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "InventoryItemName" && m.MatchedText == "Halberd");
    }

    [Fact]
    public void Search_NoResolver_SkipsItemNameSearch()
    {
        var provider = new UtmSearchProvider(); // No resolver
        var gff = UtmToGff(CreateTestUtmWithInventory());
        var criteria = new SearchCriteria { Pattern = "Longsword" };

        var matches = provider.Search(gff, criteria);

        Assert.DoesNotContain(matches, m => m.Field.Name == "InventoryItemName");
    }

    #endregion
}
