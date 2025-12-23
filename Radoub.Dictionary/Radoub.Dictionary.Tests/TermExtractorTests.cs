using Radoub.Formats.TwoDA;
using Xunit;

namespace Radoub.Dictionary.Tests;

public class TermExtractorTests
{
    [Fact]
    public void ExtractFromTwoDA_ExtractsFromLabelColumn()
    {
        var twoDA = new TwoDAFile
        {
            Columns = ["LABEL", "VALUE"],
            Rows =
            [
                new TwoDARow { Label = "0", Values = ["Fireball", "100"] },
                new TwoDARow { Label = "1", Values = ["Magic_Missile", "50"] }
            ]
        };

        var terms = TermExtractor.ExtractFromTwoDA(twoDA).ToList();

        Assert.Contains("Fireball", terms);
        Assert.Contains("Magic", terms);
        Assert.Contains("Missile", terms);
    }

    [Fact]
    public void ExtractFromTwoDA_SkipsEmptyCells()
    {
        var twoDA = new TwoDAFile
        {
            Columns = ["LABEL", "NAME"],
            Rows =
            [
                new TwoDARow { Label = "0", Values = ["Valid", null] },
                new TwoDARow { Label = "1", Values = [null, "Also_Valid"] }
            ]
        };

        var terms = TermExtractor.ExtractFromTwoDA(twoDA).ToList();

        Assert.Contains("Valid", terms);
        Assert.Contains("Also", terms);
    }

    [Fact]
    public void ExtractFromTwoDA_UsesSpecifiedColumns()
    {
        var twoDA = new TwoDAFile
        {
            Columns = ["ID", "CUSTOMCOL", "OTHER"],
            Rows =
            [
                new TwoDARow { Label = "0", Values = ["1", "ExtractThis", "SkipThis"] }
            ]
        };

        var terms = TermExtractor.ExtractFromTwoDA(twoDA, ["CUSTOMCOL"]).ToList();

        Assert.Contains("ExtractThis", terms);
        Assert.DoesNotContain("SkipThis", terms);
    }

    [Fact]
    public void ExtractFromTwoDA_SkipsShortWords()
    {
        var twoDA = new TwoDAFile
        {
            Columns = ["LABEL"],
            Rows =
            [
                new TwoDARow { Label = "0", Values = ["A"] },
                new TwoDARow { Label = "1", Values = ["LongWord"] }
            ]
        };

        var terms = TermExtractor.ExtractFromTwoDA(twoDA).ToList();

        Assert.DoesNotContain("A", terms);
        Assert.Contains("LongWord", terms);
    }

    [Fact]
    public void ExtractFromTwoDA_HandlesUnderscores()
    {
        var twoDA = new TwoDAFile
        {
            Columns = ["LABEL"],
            Rows =
            [
                new TwoDARow { Label = "0", Values = ["Fire_Bolt_Major"] }
            ]
        };

        var terms = TermExtractor.ExtractFromTwoDA(twoDA).ToList();

        Assert.Contains("Fire", terms);
        Assert.Contains("Bolt", terms);
        Assert.Contains("Major", terms);
        Assert.Contains("Fire Bolt Major", terms); // Full term
    }

    [Fact]
    public void ExtractProperNouns_ExtractsCapitalizedWords()
    {
        var text = "Welcome to Neverwinter, the jewel of the North!";

        var terms = TermExtractor.ExtractProperNouns(text).ToList();

        Assert.Contains("Welcome", terms);
        Assert.Contains("Neverwinter", terms);
        Assert.Contains("North", terms);
    }

    [Fact]
    public void ExtractProperNouns_SkipsLowercaseWords()
    {
        var text = "the quick brown fox";

        var terms = TermExtractor.ExtractProperNouns(text).ToList();

        Assert.Empty(terms);
    }

    [Fact]
    public void ExtractProperNouns_HandlesMixedCaseWords()
    {
        var text = "The MacGuffin is important.";

        var terms = TermExtractor.ExtractProperNouns(text).ToList();

        Assert.Contains("MacGuffin", terms);
    }

    [Fact]
    public void ExtractProperNouns_HandlesApostrophes()
    {
        var text = "This is D'Artagnan's sword.";

        var terms = TermExtractor.ExtractProperNouns(text).ToList();

        Assert.Contains("D'Artagnan's", terms);
    }

    [Fact]
    public void ExtractProperNouns_HandlesEmptyText()
    {
        var terms = TermExtractor.ExtractProperNouns("").ToList();
        Assert.Empty(terms);

        terms = TermExtractor.ExtractProperNouns(null!).ToList();
        Assert.Empty(terms);
    }

    [Fact]
    public void ExtractProperNouns_NoDuplicates()
    {
        var text = "Neverwinter is great. Neverwinter is beautiful. Neverwinter!";

        var terms = TermExtractor.ExtractProperNouns(text).ToList();

        Assert.Single(terms, t => t == "Neverwinter");
    }

    [Fact]
    public void ExtractProperNouns_IncludesFantasySuffixes()
    {
        var text = "The warrior traveled to Irondale and then Shadowheim.";

        var terms = TermExtractor.ExtractProperNouns(text).ToList();

        Assert.Contains("Irondale", terms);
        Assert.Contains("Shadowheim", terms);
    }
}
