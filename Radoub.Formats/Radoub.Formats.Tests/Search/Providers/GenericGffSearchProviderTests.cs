using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class GenericGffSearchProviderTests
{
    private static GffFile CreateGffWithStringFields()
    {
        var root = new GffStruct { Fields = new List<GffField>() };
        root.Fields.Add(new GffField
        {
            Label = "Tag",
            Type = GffField.CExoString,
            Value = "LOUIS_ROMAIN"
        });
        root.Fields.Add(new GffField
        {
            Label = "Comment",
            Type = GffField.CExoString,
            Value = "This is a test comment"
        });
        root.Fields.Add(new GffField
        {
            Label = "NumField",
            Type = GffField.INT,
            Value = 42
        });

        return new GffFile
        {
            FileType = "GFF ",
            FileVersion = "V3.2",
            RootStruct = root
        };
    }

    [Fact]
    public void Search_FindsCExoStringMatches()
    {
        var provider = new GenericGffSearchProvider();
        var gff = CreateGffWithStringFields();
        var criteria = new SearchCriteria { Pattern = "LOUIS" };

        var matches = provider.Search(gff, criteria);

        Assert.Single(matches);
        Assert.Equal("LOUIS_ROMAIN", matches[0].FullFieldValue);
        Assert.Equal("Tag", matches[0].Location as string);
    }

    [Fact]
    public void Search_IgnoresNonStringFields()
    {
        var provider = new GenericGffSearchProvider();
        var gff = CreateGffWithStringFields();
        var criteria = new SearchCriteria { Pattern = "42" };

        var matches = provider.Search(gff, criteria);

        Assert.Empty(matches);
    }

    [Fact]
    public void Search_FindsCExoLocStringMatches()
    {
        var root = new GffStruct { Fields = new List<GffField>() };
        var locString = new CExoLocString();
        locString.LocalizedStrings[0] = "Hello Louis Romain";
        root.Fields.Add(new GffField
        {
            Label = "FirstName",
            Type = GffField.CExoLocString,
            Value = locString
        });

        var gff = new GffFile { FileType = "UTC ", RootStruct = root };
        var provider = new GenericGffSearchProvider();
        var criteria = new SearchCriteria { Pattern = "Louis" };

        var matches = provider.Search(gff, criteria);

        Assert.Single(matches);
        Assert.Equal(0u, matches[0].LanguageId);
    }

    [Fact]
    public void Search_FindsCResRefMatches()
    {
        var root = new GffStruct
        {
            Fields = new List<GffField>
            {
                new GffField { Label = "TemplateResRef", Type = GffField.CResRef, Value = "louis_romain" }
            }
        };

        var gff = new GffFile { FileType = "UTC ", RootStruct = root };
        var provider = new GenericGffSearchProvider();
        var criteria = new SearchCriteria { Pattern = "louis" };

        var matches = provider.Search(gff, criteria);

        Assert.Single(matches);
        Assert.Equal("louis_romain", matches[0].FullFieldValue);
    }

    [Fact]
    public void Search_WalksNestedStructs()
    {
        var nested = new GffStruct
        {
            Fields = new List<GffField>
            {
                new GffField { Label = "DeepField", Type = GffField.CExoString, Value = "deep value" }
            }
        };
        var root = new GffStruct
        {
            Fields = new List<GffField>
            {
                new GffField { Label = "Child", Type = GffField.Struct, Value = nested }
            }
        };

        var gff = new GffFile { FileType = "GFF ", RootStruct = root };
        var provider = new GenericGffSearchProvider();
        var criteria = new SearchCriteria { Pattern = "deep" };

        var matches = provider.Search(gff, criteria);

        Assert.Single(matches);
        Assert.Equal("Child.DeepField", matches[0].Location as string);
    }

    [Fact]
    public void Search_WalksLists()
    {
        var listItem = new GffStruct
        {
            Fields = new List<GffField>
            {
                new GffField { Label = "Text", Type = GffField.CExoString, Value = "list item text" }
            }
        };
        var list = new GffList { Count = 1, Elements = new List<GffStruct> { listItem } };
        var root = new GffStruct
        {
            Fields = new List<GffField>
            {
                new GffField { Label = "ItemList", Type = GffField.List, Value = list }
            }
        };

        var gff = new GffFile { FileType = "GFF ", RootStruct = root };
        var provider = new GenericGffSearchProvider();
        var criteria = new SearchCriteria { Pattern = "list item" };

        var matches = provider.Search(gff, criteria);

        Assert.Single(matches);
        Assert.Equal("ItemList[0].Text", matches[0].Location as string);
    }

    [Fact]
    public void Search_MultipleMatchesInSameField()
    {
        var root = new GffStruct
        {
            Fields = new List<GffField>
            {
                new GffField { Label = "Desc", Type = GffField.CExoString, Value = "cat sat on cat mat" }
            }
        };

        var gff = new GffFile { FileType = "GFF ", RootStruct = root };
        var provider = new GenericGffSearchProvider();
        var criteria = new SearchCriteria { Pattern = "cat" };

        var matches = provider.Search(gff, criteria);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Search_CaseSensitive_RespectsFlag()
    {
        var root = new GffStruct
        {
            Fields = new List<GffField>
            {
                new GffField { Label = "Tag", Type = GffField.CExoString, Value = "LOUIS" }
            }
        };

        var gff = new GffFile { FileType = "GFF ", RootStruct = root };
        var provider = new GenericGffSearchProvider();

        var insensitive = new SearchCriteria { Pattern = "louis", CaseSensitive = false };
        Assert.Single(provider.Search(gff, insensitive));

        var sensitive = new SearchCriteria { Pattern = "louis", CaseSensitive = true };
        Assert.Empty(provider.Search(gff, sensitive));
    }

    [Fact]
    public void Search_EmptyRootStruct_ReturnsEmpty()
    {
        var gff = new GffFile { FileType = "GFF ", RootStruct = new GffStruct() };
        var provider = new GenericGffSearchProvider();
        var criteria = new SearchCriteria { Pattern = "anything" };

        var matches = provider.Search(gff, criteria);

        Assert.Empty(matches);
    }
}
