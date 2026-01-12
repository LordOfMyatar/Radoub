using Radoub.Formats.Tokens;
using Xunit;

namespace Radoub.Formats.Tests;

public class TokenParserTests
{
    private readonly TokenParser _parser = new();

    #region Plain Text Tests

    [Fact]
    public void Parse_PlainText_ReturnsSinglePlainSegment()
    {
        var result = _parser.Parse("Hello, world!");

        Assert.Single(result);
        Assert.IsType<PlainTextSegment>(result[0]);
        Assert.Equal("Hello, world!", result[0].DisplayText);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var result = _parser.Parse("");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NullString_ReturnsEmptyList()
    {
        var result = _parser.Parse(null!);

        Assert.Empty(result);
    }

    #endregion

    #region Standard Token Tests

    [Fact]
    public void Parse_FirstNameToken_ReturnsStandardToken()
    {
        var result = _parser.Parse("Hello <FirstName>!");

        Assert.Equal(3, result.Count);
        Assert.IsType<PlainTextSegment>(result[0]);
        Assert.IsType<StandardToken>(result[1]);
        Assert.IsType<PlainTextSegment>(result[2]);

        var token = (StandardToken)result[1];
        Assert.Equal("FirstName", token.TokenName);
        Assert.Equal("<FirstName>", token.RawText);
    }

    [Fact]
    public void Parse_GenderToken_ReturnsStandardToken()
    {
        var result = _parser.Parse("Well hello little <Boy/Girl>.");

        Assert.Equal(3, result.Count);
        var token = (StandardToken)result[1];
        Assert.Equal("Boy/Girl", token.TokenName);
    }

    [Fact]
    public void Parse_LowercaseGenderToken_ReturnsStandardToken()
    {
        var result = _parser.Parse("You are a good <boy/girl>.");

        Assert.Equal(3, result.Count);
        var token = (StandardToken)result[1];
        Assert.Equal("boy/girl", token.TokenName);
    }

    [Fact]
    public void Parse_MultipleStandardTokens_ReturnsAllTokens()
    {
        var result = _parser.Parse("<FirstName> the <Class>");

        Assert.Equal(3, result.Count);
        Assert.IsType<StandardToken>(result[0]);
        Assert.IsType<PlainTextSegment>(result[1]);
        Assert.IsType<StandardToken>(result[2]);

        Assert.Equal("FirstName", ((StandardToken)result[0]).TokenName);
        Assert.Equal("Class", ((StandardToken)result[2]).TokenName);
    }

    [Theory]
    [InlineData("Alignment")]
    [InlineData("Race")]
    [InlineData("Deity")]
    [InlineData("Level")]
    [InlineData("GameYear")]
    [InlineData("QuarterDay")]
    [InlineData("bitch/bastard")]
    public void Parse_AllStandardTokens_AreRecognized(string tokenName)
    {
        var result = _parser.Parse($"<{tokenName}>");

        Assert.Single(result);
        Assert.IsType<StandardToken>(result[0]);
        Assert.Equal(tokenName, ((StandardToken)result[0]).TokenName);
    }

    #endregion

    #region Custom Token Tests

    [Fact]
    public void Parse_Custom0Token_ReturnsCustomToken()
    {
        var result = _parser.Parse("<CUSTOM0>");

        Assert.Single(result);
        Assert.IsType<CustomToken>(result[0]);

        var token = (CustomToken)result[0];
        Assert.Equal(0, token.TokenNumber);
        Assert.Equal("CUSTOM0", token.TokenName);
    }

    [Fact]
    public void Parse_Custom9Token_ReturnsCustomToken()
    {
        var result = _parser.Parse("<CUSTOM9>");

        Assert.Single(result);
        var token = (CustomToken)result[0];
        Assert.Equal(9, token.TokenNumber);
    }

    [Fact]
    public void Parse_HighNumberCustomToken_ReturnsCustomToken()
    {
        var result = _parser.Parse("<CUSTOM1001>");

        Assert.Single(result);
        var token = (CustomToken)result[0];
        Assert.Equal(1001, token.TokenNumber);
    }

    #endregion

    #region Highlight Token Tests

    [Fact]
    public void Parse_ActionToken_ReturnsHighlightToken()
    {
        var result = _parser.Parse("<StartAction>[Chef waves]</Start>");

        Assert.Single(result);
        Assert.IsType<HighlightToken>(result[0]);

        var token = (HighlightToken)result[0];
        Assert.Equal(HighlightType.Action, token.Type);
        Assert.Equal("[Chef waves]", token.Content);
        Assert.Equal("[Chef waves]", token.DisplayText);
    }

    [Fact]
    public void Parse_CheckToken_ReturnsHighlightToken()
    {
        var result = _parser.Parse("<StartCheck>[Lore]</Start>");

        Assert.Single(result);
        var token = (HighlightToken)result[0];
        Assert.Equal(HighlightType.Check, token.Type);
        Assert.Equal("[Lore]", token.Content);
    }

    [Fact]
    public void Parse_HighlightToken_ReturnsHighlightToken()
    {
        var result = _parser.Parse("<StartHighlight>[Important]</Start>");

        Assert.Single(result);
        var token = (HighlightToken)result[0];
        Assert.Equal(HighlightType.Highlight, token.Type);
    }

    [Fact]
    public void Parse_ActionTokenWithSurroundingText_ParsesCorrectly()
    {
        var result = _parser.Parse("<StartAction>[Chef waves]</Start>Howdy Children!");

        Assert.Equal(2, result.Count);
        Assert.IsType<HighlightToken>(result[0]);
        Assert.IsType<PlainTextSegment>(result[1]);
        Assert.Equal("Howdy Children!", result[1].DisplayText);
    }

    [Fact]
    public void Parse_ActionTokenCaseInsensitive_ParsesCorrectly()
    {
        var result = _parser.Parse("<startaction>[waves]</start>");

        Assert.Single(result);
        Assert.IsType<HighlightToken>(result[0]);
    }

    [Fact]
    public void Parse_HighlightWithEndTag_ParsesCorrectly()
    {
        // Some content uses <End> instead of </Start>
        var result = _parser.Parse("<StartAction>[waves]<End>");

        Assert.Single(result);
        Assert.IsType<HighlightToken>(result[0]);
    }

    #endregion

    #region Color Token Tests

    [Fact]
    public void Parse_ColorToken_ReturnsColorToken()
    {
        // Create a color token with ASCII 255, 0, 0 (red)
        var text = "<c\xFF\x00\x00>red text</c>";
        var result = _parser.Parse(text);

        Assert.Single(result);
        Assert.IsType<ColorToken>(result[0]);

        var token = (ColorToken)result[0];
        Assert.Equal(255, token.Red);
        Assert.Equal(0, token.Green);
        Assert.Equal(0, token.Blue);
        Assert.Equal("red text", token.Content);
    }

    [Fact]
    public void Parse_ColorToken_ToHexColor_ReturnsCorrectHex()
    {
        var text = "<c\xFF\x80\x00>orange</c>";
        var result = _parser.Parse(text);

        var token = (ColorToken)result[0];
        Assert.Equal("#FF8000", token.ToHexColor());
    }

    #endregion

    #region User Color Token Tests

    [Fact]
    public void Parse_UserColorToken_WithConfig_ReturnsUserColorToken()
    {
        var config = new UserColorConfig
        {
            CloseToken = "<CUSTOM1000>",
            Colors = new Dictionary<string, string>
            {
                ["Red"] = "<CUSTOM1001>"
            },
            ColorHexValues = new Dictionary<string, string>
            {
                ["Red"] = "#FF0000"
            }
        };

        var parser = new TokenParser(config);
        var result = parser.Parse("<CUSTOM1001>red text<CUSTOM1000>");

        Assert.Single(result);
        Assert.IsType<UserColorToken>(result[0]);

        var token = (UserColorToken)result[0];
        Assert.Equal("Red", token.ColorName);
        Assert.Equal("red text", token.Content);
        Assert.Equal("<CUSTOM1001>", token.OpenToken);
        Assert.Equal("<CUSTOM1000>", token.CloseToken);
    }

    [Fact]
    public void Parse_UserColorToken_WithoutConfig_ReturnsCustomTokens()
    {
        // Without config, CUSTOM tokens are parsed as individual CustomToken segments
        var result = _parser.Parse("<CUSTOM1001>red text<CUSTOM1000>");

        // Should have: CUSTOM1001 token, plain text, CUSTOM1000 token
        Assert.Equal(3, result.Count);
        Assert.IsType<CustomToken>(result[0]);
        Assert.IsType<PlainTextSegment>(result[1]);
        Assert.IsType<CustomToken>(result[2]);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void Parse_MixedContent_ParsesAllTypes()
    {
        var config = UserColorConfig.CreateDefault();
        var parser = new TokenParser(config);

        var text = "Hello <FirstName>! <StartAction>[waves]</Start> You have <CUSTOM1001>red<CUSTOM1000> text.";
        var result = parser.Parse(text);

        // Should have: "Hello ", FirstName token, "! ", action token, " You have ", user color token, " text."
        Assert.True(result.Count >= 5);
        Assert.Contains(result, s => s is StandardToken st && st.TokenName == "FirstName");
        Assert.Contains(result, s => s is HighlightToken ht && ht.Type == HighlightType.Action);
        Assert.Contains(result, s => s is UserColorToken);
    }

    [Fact]
    public void Parse_NestedTokens_OuterTokenTakesPrecedence()
    {
        // Highlight tokens should take precedence and include inner content as-is
        var text = "<StartAction>[<FirstName> waves]</Start>";
        var result = _parser.Parse(text);

        Assert.Single(result);
        Assert.IsType<HighlightToken>(result[0]);
        Assert.Equal("[<FirstName> waves]", ((HighlightToken)result[0]).Content);
    }

    [Fact]
    public void Parse_UnknownAngleBracketContent_TreatedAsPlainText()
    {
        var result = _parser.Parse("<Unknown> and <Random123>");

        // Unknown tokens should be plain text
        Assert.Single(result);
        Assert.IsType<PlainTextSegment>(result[0]);
    }

    [Fact]
    public void GetDisplayText_ReturnsFormattedText()
    {
        var text = "Hello <FirstName>! <StartAction>[waves]</Start>";
        var displayText = _parser.GetDisplayText(text);

        Assert.Equal("Hello FirstName! [waves]", displayText);
    }

    #endregion

    #region Token Definitions Tests

    [Fact]
    public void TokenDefinitions_IsStandardToken_RecognizesAllTokens()
    {
        foreach (var token in TokenDefinitions.StandardTokens)
        {
            Assert.True(TokenDefinitions.IsStandardToken(token),
                $"Token '{token}' should be recognized as standard");
        }
    }

    [Fact]
    public void TokenDefinitions_IsStandardToken_RejectsUnknown()
    {
        Assert.False(TokenDefinitions.IsStandardToken("Unknown"));
        Assert.False(TokenDefinitions.IsStandardToken("CUSTOM0"));
        Assert.False(TokenDefinitions.IsStandardToken("StartAction"));
    }

    [Fact]
    public void TokenDefinitions_IsCustomToken_ParsesNumber()
    {
        Assert.True(TokenDefinitions.IsCustomToken("CUSTOM0", out int num0));
        Assert.Equal(0, num0);

        Assert.True(TokenDefinitions.IsCustomToken("CUSTOM1001", out int num1001));
        Assert.Equal(1001, num1001);

        Assert.False(TokenDefinitions.IsCustomToken("FirstName", out _));
        Assert.False(TokenDefinitions.IsCustomToken("CUSTOMabc", out _));
    }

    #endregion

    #region Color Token Encoder Tests

    [Fact]
    public void ColorTokenEncoder_EncodeColorTag_CreatesValidTag()
    {
        var tag = ColorTokenEncoder.EncodeColorTag(255, 128, 0);

        Assert.StartsWith("<c", tag);
        Assert.EndsWith(">", tag);
        Assert.Equal(6, tag.Length); // <c + 3 chars + >
    }

    [Fact]
    public void ColorTokenEncoder_DecodeRgb_DecodesCorrectly()
    {
        Assert.True(ColorTokenEncoder.DecodeRgb("ABC".AsSpan(), out byte r, out byte g, out byte b));
        Assert.Equal((byte)'A', r);
        Assert.Equal((byte)'B', g);
        Assert.Equal((byte)'C', b);
    }

    [Fact]
    public void ColorTokenEncoder_ParseHexColor_ParsesValid()
    {
        Assert.True(ColorTokenEncoder.ParseHexColor("#FF8000", out byte r, out byte g, out byte b));
        Assert.Equal(255, r);
        Assert.Equal(128, g);
        Assert.Equal(0, b);
    }

    [Fact]
    public void ColorTokenEncoder_ParseHexColor_ParsesWithoutHash()
    {
        Assert.True(ColorTokenEncoder.ParseHexColor("00FF00", out byte r, out byte g, out byte b));
        Assert.Equal(0, r);
        Assert.Equal(255, g);
        Assert.Equal(0, b);
    }

    [Fact]
    public void ColorTokenEncoder_Colorize_WrapsText()
    {
        var result = ColorTokenEncoder.Colorize("test", 255, 0, 0);

        Assert.Contains("test", result);
        Assert.StartsWith("<c", result);
        Assert.EndsWith("</c>", result);
    }

    #endregion
}
