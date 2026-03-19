using ItemEditor.Services;

namespace ItemEditor.Tests.Services;

public class ItemNamingServiceTests
{
    // --- GenerateTag ---

    [Theory]
    [InlineData("King's Longsword", "KINGS_LONGSWORD")]
    [InlineData("Armor +1", "ARMOR_1")]
    [InlineData("  spaces  ", "SPACES")]
    [InlineData("hello world", "HELLO_WORLD")]
    [InlineData("UPPER CASE", "UPPER_CASE")]
    [InlineData("already_VALID", "ALREADYVALID")]
    public void GenerateTag_StandardInput_ReturnsExpected(string input, string expected)
    {
        var result = ItemNamingService.GenerateTag(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateTag_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ItemNamingService.GenerateTag(string.Empty));
    }

    [Fact]
    public void GenerateTag_AllSymbols_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ItemNamingService.GenerateTag("!@#$%^&*()"));
    }

    [Fact]
    public void GenerateTag_LongName_TruncatesAt32()
    {
        // 35 chars of 'A' separated by spaces -> "AAAA...AAAA" all caps, truncated at 32
        var input = "abcdefghijklmnopqrstuvwxyz1234567890";
        var result = ItemNamingService.GenerateTag(input);
        Assert.True(result.Length <= 32, $"Expected <= 32 chars but got {result.Length}: '{result}'");
    }

    [Fact]
    public void GenerateTag_TruncationTrimsTrailingUnderscore()
    {
        // input: 31 a's + " b" (33 chars total)
        // after sanitize + uppercase: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA_B" (33 chars)
        // truncate at 32: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA_"
        // trim trailing '_': "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" (31 chars)
        var input = new string('a', 31) + " b";
        var result = ItemNamingService.GenerateTag(input);
        Assert.Equal(31, result.Length);
        Assert.DoesNotContain("_", result.AsSpan(result.Length - 1, 1).ToString());
    }

    [Fact]
    public void GenerateTag_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ItemNamingService.GenerateTag("   "));
    }

    // --- GenerateResRef ---

    [Theory]
    [InlineData("King's Longsword", "kings_longsword")]
    [InlineData("Armor +1", "armor_1")]
    [InlineData("UPPER CASE", "upper_case")]
    [InlineData("  spaces  ", "spaces")]
    [InlineData("hello world", "hello_world")]
    public void GenerateResRef_StandardInput_ReturnsExpected(string input, string expected)
    {
        var result = ItemNamingService.GenerateResRef(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateResRef_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ItemNamingService.GenerateResRef(string.Empty));
    }

    [Fact]
    public void GenerateResRef_AllSymbols_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ItemNamingService.GenerateResRef("!@#$%^&*()"));
    }

    [Fact]
    public void GenerateResRef_LongName_TruncatesAt16()
    {
        var input = "abcdefghijklmnopqrstuvwxyz";
        var result = ItemNamingService.GenerateResRef(input);
        Assert.True(result.Length <= 16, $"Expected <= 16 chars but got {result.Length}: '{result}'");
        Assert.Equal("abcdefghijklmnop", result);
    }

    [Fact]
    public void GenerateResRef_TruncationTrimsTrailingUnderscore()
    {
        // 15 a's + space + b = "aaaaaaaaaaaaaaa_b", truncate at 16 = "aaaaaaaaaaaaaaa_", trim = "aaaaaaaaaaaaaaa"
        var input = new string('a', 15) + " b";
        var result = ItemNamingService.GenerateResRef(input);
        Assert.True(result.Length <= 16);
        Assert.DoesNotContain("_", result.AsSpan(result.Length - 1, 1).ToString());
    }

    [Fact]
    public void GenerateResRef_ResultIsLowercase()
    {
        var result = ItemNamingService.GenerateResRef("Mixed Case Name");
        Assert.Equal(result, result.ToLower());
    }

    // --- IsValidResRef ---

    [Theory]
    [InlineData("valid_resref", true)]
    [InlineData("abc123", true)]
    [InlineData("a", true)]
    [InlineData("has space", false)]
    [InlineData("HAS_UPPER", false)]
    [InlineData("too-many-dashes", false)]
    [InlineData("", false)]
    public void IsValidResRef_ReturnsExpected(string input, bool expected)
    {
        Assert.Equal(expected, ItemNamingService.IsValidResRef(input));
    }

    [Fact]
    public void IsValidResRef_ExactlyMaxLength_ReturnsTrue()
    {
        var resref = new string('a', 16);
        Assert.True(ItemNamingService.IsValidResRef(resref));
    }

    [Fact]
    public void IsValidResRef_Over16Chars_ReturnsFalse()
    {
        var resref = new string('a', 17);
        Assert.False(ItemNamingService.IsValidResRef(resref));
    }

    // --- IsValidTag ---

    [Theory]
    [InlineData("VALID_TAG", true)]
    [InlineData("valid_tag", true)]
    [InlineData("Mixed_Case_123", true)]
    [InlineData("has space", false)]
    [InlineData("", false)]
    [InlineData("has-hyphen", false)]
    public void IsValidTag_ReturnsExpected(string input, bool expected)
    {
        Assert.Equal(expected, ItemNamingService.IsValidTag(input));
    }

    [Fact]
    public void IsValidTag_ExactlyMaxLength_ReturnsTrue()
    {
        var tag = new string('A', 32);
        Assert.True(ItemNamingService.IsValidTag(tag));
    }

    [Fact]
    public void IsValidTag_Over32Chars_ReturnsFalse()
    {
        var tag = new string('A', 33);
        Assert.False(ItemNamingService.IsValidTag(tag));
    }

    // --- ResolveResRefConflict ---

    [Fact]
    public void ResolveResRefConflict_NoConflict_ReturnsSameResRef()
    {
        var result = ItemNamingService.ResolveResRefConflict("sword", _ => false);
        Assert.Equal("sword", result);
    }

    [Fact]
    public void ResolveResRefConflict_FirstConflict_Appends001()
    {
        // "sword" exists → chop last 3 chars of "sword" = "sw", append "001" → "sw001"
        var existing = new HashSet<string> { "sword" };
        var result = ItemNamingService.ResolveResRefConflict("sword", s => existing.Contains(s));
        Assert.Equal("sw001", result);
    }

    [Fact]
    public void ResolveResRefConflict_MultipleConflicts_Increments()
    {
        var existing = new HashSet<string> { "sword", "sw001", "sw002" };
        var result = ItemNamingService.ResolveResRefConflict("sword", s => existing.Contains(s));
        Assert.Equal("sw003", result);
    }

    [Fact]
    public void ResolveResRefConflict_Exactly16Chars_StaysWithin16()
    {
        // "abcdefghijklmnop" (16 chars) exists → chop last 3 = "abcdefghijklm" (13 chars), append "001" → "abcdefghijklm001" (16 chars)
        var base16 = "abcdefghijklmnop";
        var existing = new HashSet<string> { base16 };
        var result = ItemNamingService.ResolveResRefConflict(base16, s => existing.Contains(s));
        Assert.True(result.Length <= 16, $"Result '{result}' exceeds 16 chars");
        Assert.Equal("abcdefghijklm001", result);
    }

    [Fact]
    public void ResolveResRefConflict_ShortResRef3Chars_UsesFirstChar()
    {
        // "abc" (< 4 chars) → base = first char = "a", append "001" → "a001"
        var existing = new HashSet<string> { "abc" };
        var result = ItemNamingService.ResolveResRefConflict("abc", s => existing.Contains(s));
        Assert.Equal("a001", result);
    }

    [Fact]
    public void ResolveResRefConflict_ShortResRefNoConflict_ReturnsSame()
    {
        var result = ItemNamingService.ResolveResRefConflict("ab", _ => false);
        Assert.Equal("ab", result);
    }
}
