using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class ItpSearchProviderTests
{
    /// <summary>
    /// Build a minimal ITP GFF representing:
    ///   MAIN
    ///     └─ Branch "Armor" (STRREF=6701)
    ///         ├─ Category ID=1, NAME="Medium"
    ///         │    └─ Blueprint RESREF="king_snake_robe", NAME="King Snake Robe"
    ///         └─ Category ID=2, NAME="Heavy"
    ///              └─ Blueprint RESREF="nw_aarcl005", NAME="Full Plate"
    /// </summary>
    private static GffFile CreateTestItpGff()
    {
        // Blueprint: King Snake Robe
        var blueprint1 = new GffStruct { Type = 0 };
        blueprint1.Fields = new List<GffField>
        {
            new GffField { Label = "RESREF", Type = GffField.CResRef, Value = "king_snake_robe" },
            new GffField { Label = "NAME", Type = GffField.CExoString, Value = "King Snake Robe" },
            new GffField { Label = "CR", Type = GffField.FLOAT, Value = 0.0f }
        };

        // Blueprint: Full Plate
        var blueprint2 = new GffStruct { Type = 0 };
        blueprint2.Fields = new List<GffField>
        {
            new GffField { Label = "RESREF", Type = GffField.CResRef, Value = "nw_aarcl005" },
            new GffField { Label = "NAME", Type = GffField.CExoString, Value = "Full Plate" }
        };

        // Category: Medium (ID=1, contains blueprint1)
        var category1 = new GffStruct { Type = 0 };
        category1.Fields = new List<GffField>
        {
            new GffField { Label = "ID", Type = GffField.BYTE, Value = (byte)1 },
            new GffField { Label = "NAME", Type = GffField.CExoString, Value = "Medium" },
            new GffField { Label = "LIST", Type = GffField.List, Value = new GffList { Elements = new List<GffStruct> { blueprint1 } } }
        };

        // Category: Heavy (ID=2, contains blueprint2)
        var category2 = new GffStruct { Type = 0 };
        category2.Fields = new List<GffField>
        {
            new GffField { Label = "ID", Type = GffField.BYTE, Value = (byte)2 },
            new GffField { Label = "NAME", Type = GffField.CExoString, Value = "Heavy" },
            new GffField { Label = "LIST", Type = GffField.List, Value = new GffList { Elements = new List<GffStruct> { blueprint2 } } }
        };

        // Branch: Armor (contains both categories)
        var branch = new GffStruct { Type = 0 };
        branch.Fields = new List<GffField>
        {
            new GffField { Label = "STRREF", Type = GffField.DWORD, Value = (uint)6701 },
            new GffField { Label = "NAME", Type = GffField.CExoString, Value = "Armor" },
            new GffField { Label = "LIST", Type = GffField.List, Value = new GffList { Elements = new List<GffStruct> { category1, category2 } } }
        };

        // Root with MAIN list
        var root = new GffStruct { Type = 0xFFFFFFFF };
        root.Fields = new List<GffField>
        {
            new GffField { Label = "MAIN", Type = GffField.List, Value = new GffList { Elements = new List<GffStruct> { branch } } }
        };

        return new GffFile { FileType = "ITP ", FileVersion = "V3.2", RootStruct = root };
    }

    [Fact]
    public void Search_FindsBlueprintByResRef()
    {
        var provider = new ItpSearchProvider();
        var gff = CreateTestItpGff();
        var criteria = new SearchCriteria { Pattern = "king_snake_robe" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "ResRef" &&
            m.MatchedText == "king_snake_robe");
    }

    [Fact]
    public void Search_FindsBlueprintByName()
    {
        var provider = new ItpSearchProvider();
        var gff = CreateTestItpGff();
        var criteria = new SearchCriteria { Pattern = "King Snake Robe" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "Name" &&
            m.MatchedText == "King Snake Robe");
    }

    [Fact]
    public void Search_DisplayPathShowsHierarchy()
    {
        var provider = new ItpSearchProvider();
        var gff = CreateTestItpGff();
        var criteria = new SearchCriteria { Pattern = "king_snake_robe" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches, m => m.Field.Name == "ResRef");
        var location = match.Location as ItpMatchLocation;
        Assert.NotNull(location);
        Assert.Equal("Armor \u2192 Medium \u2192 King Snake Robe", location.DisplayPath);
    }

    [Fact]
    public void Search_FindsCategoryName()
    {
        var provider = new ItpSearchProvider();
        var gff = CreateTestItpGff();
        var criteria = new SearchCriteria { Pattern = "Medium" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "Category Name" &&
            m.MatchedText == "Medium");
    }

    [Fact]
    public void Search_FindsBranchName()
    {
        var provider = new ItpSearchProvider();
        var gff = CreateTestItpGff();
        var criteria = new SearchCriteria { Pattern = "Armor" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "Branch Name" &&
            m.MatchedText == "Armor");
    }

    [Fact]
    public void Search_MultipleBlueprintsFound()
    {
        var provider = new ItpSearchProvider();
        var gff = CreateTestItpGff();
        var criteria = new SearchCriteria { Pattern = "nw_", IsRegex = false };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.FullFieldValue == "nw_aarcl005");
    }

    [Fact]
    public void Search_ResRefFieldIsNotReplaceable()
    {
        var provider = new ItpSearchProvider();
        var gff = CreateTestItpGff();
        var criteria = new SearchCriteria { Pattern = "king_snake_robe" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches, m => m.Field.Name == "ResRef");
        Assert.False(match.Field.IsReplaceable);
    }
}
