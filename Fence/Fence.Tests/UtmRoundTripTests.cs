using Radoub.Formats.Gff;
using Radoub.Formats.Utm;

namespace Fence.Tests;

/// <summary>
/// Tests for UTM file round-trip validation.
/// Ensures all store properties are correctly read and written to disk.
/// Issue #564: Real UTM file validation tests
/// </summary>
public class UtmRoundTripTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _testFiles = new();

    public UtmRoundTripTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "FenceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up test files
        foreach (var file in _testFiles)
        {
            if (File.Exists(file))
                File.Delete(file);
        }

        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);

        GC.SuppressFinalize(this);
    }

    private string CreateTestFilePath(string name)
    {
        var path = Path.Combine(_testDirectory, name + ".utm");
        _testFiles.Add(path);
        return path;
    }

    #region Identity Fields

    [Fact]
    public void RoundTrip_ResRef_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("resref_test");
        var original = new UtmFile { ResRef = "test_store_01" };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal("test_store_01", loaded.ResRef);
    }

    [Fact]
    public void RoundTrip_Tag_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("tag_test");
        var original = new UtmFile
        {
            ResRef = "test_store",
            Tag = "UNIQUE_STORE_TAG_123"
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal("UNIQUE_STORE_TAG_123", loaded.Tag);
    }

    [Fact]
    public void RoundTrip_LocalizedName_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("locname_test");
        var original = new UtmFile { ResRef = "test_store" };
        original.LocName.SetString(0, "Blacksmith's Forge");

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal("Blacksmith's Forge", loaded.LocName.GetDefault());
    }

    [Fact]
    public void RoundTrip_EmptyName_HandledCorrectly()
    {
        var path = CreateTestFilePath("emptyname_test");
        var original = new UtmFile { ResRef = "empty_store" };
        // LocName left as default (empty)

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.True(loaded.LocName.IsEmpty || string.IsNullOrEmpty(loaded.LocName.GetDefault()));
    }

    #endregion

    #region Pricing Fields

    [Theory]
    [InlineData(50)]   // Default markdown
    [InlineData(0)]    // No markdown (free buying)
    [InlineData(100)]  // Full price
    [InlineData(25)]   // Low markdown
    [InlineData(75)]   // High markdown
    public void RoundTrip_MarkDown_PreservedThroughFileSave(int markDown)
    {
        var path = CreateTestFilePath($"markdown_{markDown}");
        var original = new UtmFile
        {
            ResRef = "test_store",
            MarkDown = markDown
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(markDown, loaded.MarkDown);
    }

    [Theory]
    [InlineData(100)]  // Base price
    [InlineData(150)]  // 50% markup
    [InlineData(200)]  // Double price
    [InlineData(50)]   // Discount (rare but valid)
    [InlineData(500)]  // Very expensive
    public void RoundTrip_MarkUp_PreservedThroughFileSave(int markUp)
    {
        var path = CreateTestFilePath($"markup_{markUp}");
        var original = new UtmFile
        {
            ResRef = "test_store",
            MarkUp = markUp
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(markUp, loaded.MarkUp);
    }

    [Theory]
    [InlineData(-1)]      // Infinite gold (default)
    [InlineData(0)]       // No gold (cannot buy)
    [InlineData(1000)]    // Small reserve
    [InlineData(100000)]  // Large reserve
    [InlineData(999999)]  // Very large reserve
    public void RoundTrip_StoreGold_PreservedThroughFileSave(int storeGold)
    {
        var path = CreateTestFilePath($"gold_{storeGold}");
        var original = new UtmFile
        {
            ResRef = "test_store",
            StoreGold = storeGold
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(storeGold, loaded.StoreGold);
    }

    [Theory]
    [InlineData(-1)]     // No limit (default)
    [InlineData(0)]      // Won't buy anything
    [InlineData(500)]    // Low limit
    [InlineData(10000)]  // High limit
    [InlineData(100000)] // Very high limit
    public void RoundTrip_MaxBuyPrice_PreservedThroughFileSave(int maxBuyPrice)
    {
        var path = CreateTestFilePath($"maxbuy_{maxBuyPrice}");
        var original = new UtmFile
        {
            ResRef = "test_store",
            MaxBuyPrice = maxBuyPrice
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(maxBuyPrice, loaded.MaxBuyPrice);
    }

    [Theory]
    [InlineData(100)]  // Default
    [InlineData(0)]    // Free identification
    [InlineData(50)]   // Cheap
    [InlineData(500)]  // Expensive
    [InlineData(-1)]   // Service unavailable
    public void RoundTrip_IdentifyPrice_PreservedThroughFileSave(int identifyPrice)
    {
        var path = CreateTestFilePath($"identify_{identifyPrice}");
        var original = new UtmFile
        {
            ResRef = "test_store",
            IdentifyPrice = identifyPrice
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(identifyPrice, loaded.IdentifyPrice);
    }

    #endregion

    #region Black Market Fields

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RoundTrip_BlackMarket_PreservedThroughFileSave(bool blackMarket)
    {
        var path = CreateTestFilePath($"blackmarket_{blackMarket}");
        var original = new UtmFile
        {
            ResRef = "test_store",
            BlackMarket = blackMarket
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(blackMarket, loaded.BlackMarket);
    }

    [Theory]
    [InlineData(25)]  // Default
    [InlineData(0)]   // No markdown for stolen
    [InlineData(50)]  // Higher rate
    [InlineData(10)]  // Low rate (typical fence)
    public void RoundTrip_BM_MarkDown_PreservedThroughFileSave(int bmMarkDown)
    {
        var path = CreateTestFilePath($"bmmarkdown_{bmMarkDown}");
        var original = new UtmFile
        {
            ResRef = "test_store",
            BlackMarket = true,
            BM_MarkDown = bmMarkDown
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(bmMarkDown, loaded.BM_MarkDown);
    }

    #endregion

    #region Blueprint Fields

    [Fact]
    public void RoundTrip_Comment_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("comment_test");
        var comment = "This is a blacksmith store for the village. Opens at dawn, closes at dusk.";
        var original = new UtmFile
        {
            ResRef = "test_store",
            Comment = comment
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(comment, loaded.Comment);
    }

    [Fact]
    public void RoundTrip_EmptyComment_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("emptycomment_test");
        var original = new UtmFile
        {
            ResRef = "test_store",
            Comment = string.Empty
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(string.Empty, loaded.Comment);
    }

    [Theory]
    [InlineData(0)]   // Default category
    [InlineData(1)]   // Custom 1
    [InlineData(5)]   // Custom 5
    [InlineData(255)] // Max value
    public void RoundTrip_PaletteID_PreservedThroughFileSave(byte paletteId)
    {
        var path = CreateTestFilePath($"palette_{paletteId}");
        var original = new UtmFile
        {
            ResRef = "test_store",
            PaletteID = paletteId
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(paletteId, loaded.PaletteID);
    }

    #endregion

    #region Script Fields

    [Fact]
    public void RoundTrip_OnOpenStore_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("onopen_test");
        var original = new UtmFile
        {
            ResRef = "test_store",
            OnOpenStore = "my_store_open"
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal("my_store_open", loaded.OnOpenStore);
    }

    [Fact]
    public void RoundTrip_OnStoreClosed_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("onclose_test");
        var original = new UtmFile
        {
            ResRef = "test_store",
            OnStoreClosed = "my_store_close"
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal("my_store_close", loaded.OnStoreClosed);
    }

    [Fact]
    public void RoundTrip_BothScripts_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("bothscripts_test");
        var original = new UtmFile
        {
            ResRef = "test_store",
            OnOpenStore = "store_on_open",
            OnStoreClosed = "store_on_close"
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal("store_on_open", loaded.OnOpenStore);
        Assert.Equal("store_on_close", loaded.OnStoreClosed);
    }

    [Fact]
    public void RoundTrip_NoScripts_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("noscripts_test");
        var original = new UtmFile
        {
            ResRef = "test_store",
            OnOpenStore = string.Empty,
            OnStoreClosed = string.Empty
        };

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(string.Empty, loaded.OnOpenStore);
        Assert.Equal(string.Empty, loaded.OnStoreClosed);
    }

    #endregion

    #region Store Inventory

    [Fact]
    public void RoundTrip_SingleItem_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("singleitem_test");
        var original = new UtmFile { ResRef = "test_store" };
        original.StoreList.Add(new StorePanel
        {
            PanelId = StorePanels.Weapons,
            Items = new List<StoreItem>
            {
                new StoreItem { InventoryRes = "nw_wswls001", Infinite = true }
            }
        });

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Single(loaded.StoreList);
        Assert.Single(loaded.StoreList[0].Items);
        Assert.Equal("nw_wswls001", loaded.StoreList[0].Items[0].InventoryRes);
        Assert.True(loaded.StoreList[0].Items[0].Infinite);
    }

    [Fact]
    public void RoundTrip_MultipleItemsMultiplePanels_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("multipleitems_test");
        var original = new UtmFile { ResRef = "test_store" };

        // Weapons panel
        original.StoreList.Add(new StorePanel
        {
            PanelId = StorePanels.Weapons,
            Items = new List<StoreItem>
            {
                new StoreItem { InventoryRes = "nw_wswls001", Infinite = true },
                new StoreItem { InventoryRes = "nw_wswls002", Infinite = false }
            }
        });

        // Armor panel
        original.StoreList.Add(new StorePanel
        {
            PanelId = StorePanels.Armor,
            Items = new List<StoreItem>
            {
                new StoreItem { InventoryRes = "nw_aarcl001", Infinite = true }
            }
        });

        // Potions panel
        original.StoreList.Add(new StorePanel
        {
            PanelId = StorePanels.Potions,
            Items = new List<StoreItem>
            {
                new StoreItem { InventoryRes = "nw_it_mpotion001", Infinite = true },
                new StoreItem { InventoryRes = "nw_it_mpotion002", Infinite = true },
                new StoreItem { InventoryRes = "nw_it_mpotion003", Infinite = false }
            }
        });

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(3, loaded.StoreList.Count);

        // Verify weapons
        var weapons = loaded.StoreList.First(p => p.PanelId == StorePanels.Weapons);
        Assert.Equal(2, weapons.Items.Count);

        // Verify armor
        var armor = loaded.StoreList.First(p => p.PanelId == StorePanels.Armor);
        Assert.Single(armor.Items);

        // Verify potions
        var potions = loaded.StoreList.First(p => p.PanelId == StorePanels.Potions);
        Assert.Equal(3, potions.Items.Count);
    }

    [Fact]
    public void RoundTrip_EmptyStore_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("emptystore_test");
        var original = new UtmFile { ResRef = "empty_store" };
        // No items added

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Empty(loaded.StoreList);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RoundTrip_InfiniteFlag_PreservedThroughFileSave(bool infinite)
    {
        var path = CreateTestFilePath($"infinite_{infinite}");
        var original = new UtmFile { ResRef = "test_store" };
        original.StoreList.Add(new StorePanel
        {
            PanelId = StorePanels.Weapons,
            Items = new List<StoreItem>
            {
                new StoreItem { InventoryRes = "test_item", Infinite = infinite }
            }
        });

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(infinite, loaded.StoreList[0].Items[0].Infinite);
    }

    #endregion

    #region Buy Restrictions

    [Fact]
    public void RoundTrip_WillOnlyBuy_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("willonlybuy_test");
        var original = new UtmFile { ResRef = "test_store" };
        original.WillOnlyBuy.AddRange(new[] { 4, 5, 6, 10 }); // Swords and daggers

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(4, loaded.WillOnlyBuy.Count);
        Assert.Contains(4, loaded.WillOnlyBuy);
        Assert.Contains(5, loaded.WillOnlyBuy);
        Assert.Contains(6, loaded.WillOnlyBuy);
        Assert.Contains(10, loaded.WillOnlyBuy);
    }

    [Fact]
    public void RoundTrip_WillNotBuy_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("willnotbuy_test");
        var original = new UtmFile { ResRef = "test_store" };
        original.WillNotBuy.AddRange(new[] { 49, 74, 75 }); // Potions and scrolls

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(3, loaded.WillNotBuy.Count);
        Assert.Contains(49, loaded.WillNotBuy);
        Assert.Contains(74, loaded.WillNotBuy);
        Assert.Contains(75, loaded.WillNotBuy);
    }

    [Fact]
    public void RoundTrip_NoRestrictions_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("norestrictions_test");
        var original = new UtmFile { ResRef = "test_store" };
        // No restrictions added

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Empty(loaded.WillOnlyBuy);
        Assert.Empty(loaded.WillNotBuy);
    }

    #endregion

    #region Variables

    [Fact]
    public void RoundTrip_IntVariable_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("intvar_test");
        var original = new UtmFile { ResRef = "test_store" };
        original.VarTable.Add(Variable.CreateInt("StoreLevel", 5));

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Single(loaded.VarTable);
        var variable = loaded.VarTable[0];
        Assert.Equal("StoreLevel", variable.Name);
        Assert.Equal(VariableType.Int, variable.Type);
        Assert.Equal(5, variable.GetInt());
    }

    [Fact]
    public void RoundTrip_FloatVariable_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("floatvar_test");
        var original = new UtmFile { ResRef = "test_store" };
        original.VarTable.Add(Variable.CreateFloat("Discount", 0.15f));

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Single(loaded.VarTable);
        var variable = loaded.VarTable[0];
        Assert.Equal("Discount", variable.Name);
        Assert.Equal(VariableType.Float, variable.Type);
        Assert.Equal(0.15f, variable.GetFloat(), precision: 3);
    }

    [Fact]
    public void RoundTrip_StringVariable_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("stringvar_test");
        var original = new UtmFile { ResRef = "test_store" };
        original.VarTable.Add(Variable.CreateString("OwnerName", "Elminster"));

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Single(loaded.VarTable);
        var variable = loaded.VarTable[0];
        Assert.Equal("OwnerName", variable.Name);
        Assert.Equal(VariableType.String, variable.Type);
        Assert.Equal("Elminster", variable.GetString());
    }

    [Fact]
    public void RoundTrip_MultipleVariables_PreservedThroughFileSave()
    {
        var path = CreateTestFilePath("multivars_test");
        var original = new UtmFile { ResRef = "test_store" };
        original.VarTable.Add(Variable.CreateInt("Level", 10));
        original.VarTable.Add(Variable.CreateFloat("TaxRate", 0.05f));
        original.VarTable.Add(Variable.CreateString("Region", "Neverwinter"));

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        Assert.Equal(3, loaded.VarTable.Count);
        Assert.Contains(loaded.VarTable, v => v.Name == "Level" && v.GetInt() == 10);
        Assert.Contains(loaded.VarTable, v => v.Name == "TaxRate" && Math.Abs(v.GetFloat() - 0.05f) < 0.001f);
        Assert.Contains(loaded.VarTable, v => v.Name == "Region" && v.GetString() == "Neverwinter");
    }

    #endregion

    #region Complete Store Tests

    [Fact]
    public void RoundTrip_CompleteStore_AllFieldsPreserved()
    {
        var path = CreateTestFilePath("complete_store");
        var original = CreateCompleteStore();

        UtmWriter.Write(original, path);
        var loaded = UtmReader.Read(path);

        // Identity
        Assert.Equal(original.ResRef, loaded.ResRef);
        Assert.Equal(original.Tag, loaded.Tag);
        Assert.Equal(original.LocName.GetDefault(), loaded.LocName.GetDefault());

        // Pricing
        Assert.Equal(original.MarkDown, loaded.MarkDown);
        Assert.Equal(original.MarkUp, loaded.MarkUp);
        Assert.Equal(original.StoreGold, loaded.StoreGold);
        Assert.Equal(original.MaxBuyPrice, loaded.MaxBuyPrice);
        Assert.Equal(original.IdentifyPrice, loaded.IdentifyPrice);

        // Black market
        Assert.Equal(original.BlackMarket, loaded.BlackMarket);
        Assert.Equal(original.BM_MarkDown, loaded.BM_MarkDown);

        // Blueprint
        Assert.Equal(original.Comment, loaded.Comment);
        Assert.Equal(original.PaletteID, loaded.PaletteID);

        // Scripts
        Assert.Equal(original.OnOpenStore, loaded.OnOpenStore);
        Assert.Equal(original.OnStoreClosed, loaded.OnStoreClosed);

        // Inventory
        Assert.Equal(original.StoreList.Count, loaded.StoreList.Count);
        for (int i = 0; i < original.StoreList.Count; i++)
        {
            Assert.Equal(original.StoreList[i].PanelId, loaded.StoreList[i].PanelId);
            Assert.Equal(original.StoreList[i].Items.Count, loaded.StoreList[i].Items.Count);
        }

        // Restrictions
        Assert.Equal(original.WillOnlyBuy.Count, loaded.WillOnlyBuy.Count);
        Assert.Equal(original.WillNotBuy.Count, loaded.WillNotBuy.Count);

        // Variables
        Assert.Equal(original.VarTable.Count, loaded.VarTable.Count);
    }

    [Fact]
    public void RoundTrip_MultipleWritesReads_DataRemainsConsistent()
    {
        var path = CreateTestFilePath("multipass_store");
        var original = CreateCompleteStore();

        // Write and read multiple times
        UtmWriter.Write(original, path);
        var pass1 = UtmReader.Read(path);

        UtmWriter.Write(pass1, path);
        var pass2 = UtmReader.Read(path);

        UtmWriter.Write(pass2, path);
        var pass3 = UtmReader.Read(path);

        // All passes should have identical data
        Assert.Equal(original.ResRef, pass3.ResRef);
        Assert.Equal(original.MarkDown, pass3.MarkDown);
        Assert.Equal(original.MarkUp, pass3.MarkUp);
        Assert.Equal(original.Comment, pass3.Comment);
        Assert.Equal(original.StoreList.Count, pass3.StoreList.Count);
        Assert.Equal(original.VarTable.Count, pass3.VarTable.Count);
    }

    private static UtmFile CreateCompleteStore()
    {
        var store = new UtmFile
        {
            ResRef = "blacksmith_01",
            Tag = "BLACKSMITH_VILLAGE",
            MarkDown = 60,
            MarkUp = 140,
            StoreGold = 5000,
            MaxBuyPrice = 10000,
            IdentifyPrice = 50,
            BlackMarket = true,
            BM_MarkDown = 15,
            Comment = "Village blacksmith. Specializes in weapons and armor.",
            PaletteID = 1,
            OnOpenStore = "store_open_scr",
            OnStoreClosed = "store_close_scr"
        };

        store.LocName.SetString(0, "Grimjaw's Smithy");

        // Add inventory
        store.StoreList.Add(new StorePanel
        {
            PanelId = StorePanels.Weapons,
            Items = new List<StoreItem>
            {
                new StoreItem { InventoryRes = "nw_wswls001", Infinite = true },
                new StoreItem { InventoryRes = "nw_wswss001", Infinite = true },
                new StoreItem { InventoryRes = "nw_waxbt001", Infinite = false }
            }
        });

        store.StoreList.Add(new StorePanel
        {
            PanelId = StorePanels.Armor,
            Items = new List<StoreItem>
            {
                new StoreItem { InventoryRes = "nw_aarcl001", Infinite = true },
                new StoreItem { InventoryRes = "nw_ashsw001", Infinite = true }
            }
        });

        // Buy restrictions - only weapons and armor
        store.WillOnlyBuy.AddRange(new[] { 4, 5, 6, 10, 14, 16 }); // Various weapon types
        store.WillNotBuy.AddRange(new[] { 49, 74 }); // Won't buy potions or scrolls

        // Variables
        store.VarTable.Add(Variable.CreateInt("RepairSkill", 15));
        store.VarTable.Add(Variable.CreateString("OwnerRace", "Dwarf"));

        return store;
    }

    #endregion
}
