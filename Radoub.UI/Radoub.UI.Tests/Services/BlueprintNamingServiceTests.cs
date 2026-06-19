using System.Collections.Generic;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

/// <summary>
/// Contract tests for the shared BlueprintNamingService (#2418). Ported from Relique's
/// ItemNamingServiceTests so the extracted shared service preserves the exact behavior the
/// per-tool copies had before migration.
/// </summary>
public class BlueprintNamingServiceTests
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
        Assert.Equal(expected, BlueprintNamingService.GenerateTag(input));
    }

    [Fact]
    public void GenerateTag_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BlueprintNamingService.GenerateTag(string.Empty));
    }

    [Fact]
    public void GenerateTag_AllSymbols_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BlueprintNamingService.GenerateTag("!@#$%^&*()"));
    }

    [Fact]
    public void GenerateTag_LongName_TruncatesAt32()
    {
        var input = "abcdefghijklmnopqrstuvwxyz1234567890";
        var result = BlueprintNamingService.GenerateTag(input);
        Assert.True(result.Length <= 32, $"Expected <= 32 chars but got {result.Length}: '{result}'");
    }

    [Fact]
    public void GenerateTag_TruncationTrimsTrailingUnderscore()
    {
        var input = new string('a', 31) + " b";
        var result = BlueprintNamingService.GenerateTag(input);
        Assert.Equal(31, result.Length);
        Assert.DoesNotContain("_", result.AsSpan(result.Length - 1, 1).ToString());
    }

    [Fact]
    public void GenerateTag_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BlueprintNamingService.GenerateTag("   "));
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
        Assert.Equal(expected, BlueprintNamingService.GenerateResRef(input));
    }

    [Fact]
    public void GenerateResRef_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BlueprintNamingService.GenerateResRef(string.Empty));
    }

    [Fact]
    public void GenerateResRef_AllSymbols_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, BlueprintNamingService.GenerateResRef("!@#$%^&*()"));
    }

    [Fact]
    public void GenerateResRef_LongName_TruncatesAt16()
    {
        var input = "abcdefghijklmnopqrstuvwxyz";
        var result = BlueprintNamingService.GenerateResRef(input);
        Assert.True(result.Length <= 16, $"Expected <= 16 chars but got {result.Length}: '{result}'");
        Assert.Equal("abcdefghijklmnop", result);
    }

    [Fact]
    public void GenerateResRef_TruncationTrimsTrailingUnderscore()
    {
        var input = new string('a', 15) + " b";
        var result = BlueprintNamingService.GenerateResRef(input);
        Assert.True(result.Length <= 16);
        Assert.DoesNotContain("_", result.AsSpan(result.Length - 1, 1).ToString());
    }

    [Fact]
    public void GenerateResRef_ResultIsLowercase()
    {
        var result = BlueprintNamingService.GenerateResRef("Mixed Case Name");
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
        Assert.Equal(expected, BlueprintNamingService.IsValidResRef(input));
    }

    [Fact]
    public void IsValidResRef_ExactlyMaxLength_ReturnsTrue()
    {
        Assert.True(BlueprintNamingService.IsValidResRef(new string('a', 16)));
    }

    [Fact]
    public void IsValidResRef_Over16Chars_ReturnsFalse()
    {
        Assert.False(BlueprintNamingService.IsValidResRef(new string('a', 17)));
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
        Assert.Equal(expected, BlueprintNamingService.IsValidTag(input));
    }

    [Fact]
    public void IsValidTag_ExactlyMaxLength_ReturnsTrue()
    {
        Assert.True(BlueprintNamingService.IsValidTag(new string('A', 32)));
    }

    [Fact]
    public void IsValidTag_Over32Chars_ReturnsFalse()
    {
        Assert.False(BlueprintNamingService.IsValidTag(new string('A', 33)));
    }

    // --- ResolveResRefConflict ---

    [Fact]
    public void ResolveResRefConflict_NoConflict_ReturnsSameResRef()
    {
        Assert.Equal("sword", BlueprintNamingService.ResolveResRefConflict("sword", _ => false));
    }

    [Fact]
    public void ResolveResRefConflict_FirstConflict_Appends001()
    {
        var existing = new HashSet<string> { "sword" };
        Assert.Equal("sw001", BlueprintNamingService.ResolveResRefConflict("sword", s => existing.Contains(s)));
    }

    [Fact]
    public void ResolveResRefConflict_MultipleConflicts_Increments()
    {
        var existing = new HashSet<string> { "sword", "sw001", "sw002" };
        Assert.Equal("sw003", BlueprintNamingService.ResolveResRefConflict("sword", s => existing.Contains(s)));
    }

    [Fact]
    public void ResolveResRefConflict_Exactly16Chars_StaysWithin16()
    {
        var base16 = "abcdefghijklmnop";
        var existing = new HashSet<string> { base16 };
        var result = BlueprintNamingService.ResolveResRefConflict(base16, s => existing.Contains(s));
        Assert.True(result.Length <= 16, $"Result '{result}' exceeds 16 chars");
        Assert.Equal("abcdefghijklm001", result);
    }

    [Fact]
    public void ResolveResRefConflict_ShortResRef3Chars_UsesFirstChar()
    {
        var existing = new HashSet<string> { "abc" };
        Assert.Equal("a001", BlueprintNamingService.ResolveResRefConflict("abc", s => existing.Contains(s)));
    }

    [Fact]
    public void ResolveResRefConflict_ShortResRefNoConflict_ReturnsSame()
    {
        Assert.Equal("ab", BlueprintNamingService.ResolveResRefConflict("ab", _ => false));
    }
}
