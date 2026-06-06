using Radoub.Formats.Search.Rename;
using Xunit;

namespace Radoub.Formats.Tests.Search.Rename;

public class ResRefValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Validate_EmptyOrWhitespace_ReturnsFailWithEmptyError(string input)
    {
        var validator = new ResRefValidator();
        var result = validator.Validate(input, existingNames: new HashSet<string>(), extension: ".dlg");
        Assert.False(result.IsValid);
        Assert.Equal("ResRef cannot be empty", result.Error);
    }

    [Theory]
    [InlineData("louis.dlg", "louis")]
    [InlineData("louis.DLG", "louis")]
    [InlineData("louis", "louis")]
    public void Validate_StripsExtension(string input, string expectedName)
    {
        var validator = new ResRefValidator();
        var result = validator.Validate(input, new HashSet<string>(), ".dlg");
        Assert.True(result.IsValid);
        Assert.Equal(expectedName, result.NormalizedName);
    }

    [Fact]
    public void Validate_TooLong_ReturnsFailWithLengthInError()
    {
        var validator = new ResRefValidator();
        var result = validator.Validate("12345678901234567", new HashSet<string>(), ".dlg");
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("17", result.Error);
        Assert.Contains("16", result.Error);
    }

    [Theory]
    [InlineData("louis-roumain")]   // hyphen
    [InlineData("louis roumain")]   // space
    [InlineData("louis.roumain")]   // internal dot — fails because .roumain is not the extension being stripped
    [InlineData("louis@home")]      // symbol
    [InlineData("louisé")]          // non-ASCII
    public void Validate_InvalidCharacters_ReturnsFail(string input)
    {
        var validator = new ResRefValidator();
        var result = validator.Validate(input, new HashSet<string>(), ".dlg");
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("lowercase letters, digits, and underscores", result.Error);
    }

    // #2182 — length error should suggest a 16-char truncation the user can use.
    [Fact]
    public void Validate_TooLong_ErrorSuggestsTruncatedName()
    {
        var validator = new ResRefValidator();
        var result = validator.Validate("longitem_blueprint_extra", new HashSet<string>(), ".uti");
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        // Suggests the first 16 characters of the normalized name.
        Assert.Contains("longitem_bluepri", result.Error);
    }

    // #2182 — char error should name the offending characters, not just the rule.
    [Theory]
    [InlineData("louis-roumain", "'-'")]
    [InlineData("louis roumain", "space")]
    [InlineData("louis@home", "'@'")]
    public void Validate_InvalidCharacters_ErrorNamesTheBadChars(string input, string badCharText)
    {
        var validator = new ResRefValidator();
        var result = validator.Validate(input, new HashSet<string>(), ".dlg");
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains(badCharText, result.Error);
    }

    [Fact]
    public void Validate_StartsWithDigit_ReturnsOkWithWarning()
    {
        var validator = new ResRefValidator();
        var result = validator.Validate("9lives", new HashSet<string>(), ".dlg");
        Assert.True(result.IsValid);
        Assert.Equal("9lives", result.NormalizedName);
        Assert.NotNull(result.Warning);
        Assert.Contains("starts with a digit", result.Warning);
    }

    [Fact]
    public void Validate_NoCollision_ReturnsOkWithNoSuffix()
    {
        var validator = new ResRefValidator();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "alice", "bob" };
        var result = validator.Validate("charlie", existing, ".dlg");
        Assert.True(result.IsValid);
        Assert.Equal("charlie", result.NormalizedName);
        Assert.False(result.AutoSuffixApplied);
    }

    [Fact]
    public void Validate_CollisionExists_AppliesUnderscore2Suffix()
    {
        var validator = new ResRefValidator();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "louis" };
        var result = validator.Validate("louis", existing, ".dlg");
        Assert.True(result.IsValid);
        Assert.Equal("louis_2", result.NormalizedName);
        Assert.True(result.AutoSuffixApplied);
    }

    [Fact]
    public void Validate_CollisionIsCaseInsensitive()
    {
        var validator = new ResRefValidator();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "louis" };
        var result = validator.Validate("LOUIS", existing, ".dlg");
        Assert.True(result.IsValid);
        Assert.Equal("louis_2", result.NormalizedName);
        Assert.True(result.AutoSuffixApplied);
    }

    [Fact]
    public void Validate_MultipleCollisions_FindsLowestAvailableSuffix()
    {
        var validator = new ResRefValidator();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "louis", "louis_2", "louis_3", "louis_4", "louis_5"
        };
        var result = validator.Validate("louis", existing, ".dlg");
        Assert.True(result.IsValid);
        Assert.Equal("louis_6", result.NormalizedName);
        Assert.True(result.AutoSuffixApplied);
    }

    [Fact]
    public void Validate_AllSuffixesTaken_ReturnsFail()
    {
        var validator = new ResRefValidator();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "louis" };
        for (int i = 2; i <= 99; i++) existing.Add($"louis_{i}");
        var result = validator.Validate("louis", existing, ".dlg");
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Contains("_99", result.Error);
    }

    [Fact]
    public void Validate_LongBaseName_TruncatesToFitSuffix()
    {
        var validator = new ResRefValidator();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "abcdefghijklmnop" };
        var result = validator.Validate("abcdefghijklmnop", existing, ".dlg");
        Assert.True(result.IsValid);
        Assert.Equal("abcdefghijklmn_2", result.NormalizedName);  // 14 chars + _2 = 16
        Assert.True(result.AutoSuffixApplied);
    }

    [Fact]
    public void Validate_TruncatedNameAlsoCollides_TriesNextSuffix()
    {
        var validator = new ResRefValidator();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abcdefghijklmnop",
            "abcdefghijklmn_2"
        };
        var result = validator.Validate("abcdefghijklmnop", existing, ".dlg");
        Assert.True(result.IsValid);
        Assert.Equal("abcdefghijklmn_3", result.NormalizedName);
        Assert.True(result.AutoSuffixApplied);
    }

    [Fact]
    public void Validate_RenameToSameName_NoCollisionWhenCallerExcludesOldName()
    {
        // Contract: caller is responsible for excluding the name being renamed
        // from existingNames. When the caller does so, "renaming" louis -> louis
        // is a no-op rename and validates without auto-suffix.
        var validator = new ResRefValidator();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "alice", "bob" };
        // Note: "louis" is intentionally NOT in `existing` (caller filtered it out)

        var result = validator.Validate("louis", existing, ".dlg");

        Assert.True(result.IsValid);
        Assert.Equal("louis", result.NormalizedName);
        Assert.False(result.AutoSuffixApplied);
    }
}
