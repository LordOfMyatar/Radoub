using PlaceableEditor.Services;

namespace PlaceableEditor.Tests.Services;

/// <summary>
/// PlaceableNamingService (#2372) mirrors Relique's ItemNamingService: derive a NWN Tag (UPPERCASE,
/// ≤32) and ResRef (lowercase, ≤16) from a display name. Used by the Identity panel's "sync with
/// name" checkboxes so naming a placeable doesn't require typing Tag/ResRef by hand.
/// </summary>
public class PlaceableNamingServiceTests
{
    [Theory]
    [InlineData("Treasure Chest", "TREASURE_CHEST")]
    [InlineData("oak door!!!", "OAK_DOOR")]
    [InlineData("  spaced  out  ", "SPACED_OUT")]
    [InlineData("", "")]
    public void GenerateTag_UppercasesSanitizesAndUnderscores(string name, string expected)
        => Assert.Equal(expected, PlaceableNamingService.GenerateTag(name));

    [Theory]
    [InlineData("Treasure Chest", "treasure_chest")]
    [InlineData("oak door!!!", "oak_door")]
    [InlineData("", "")]
    public void GenerateResRef_LowercasesSanitizesAndUnderscores(string name, string expected)
        => Assert.Equal(expected, PlaceableNamingService.GenerateResRef(name));

    [Fact]
    public void GenerateResRef_TruncatesTo16Chars()
    {
        var resRef = PlaceableNamingService.GenerateResRef("A Very Long Placeable Name Indeed");
        Assert.True(resRef.Length <= 16, $"ResRef '{resRef}' exceeds 16 chars");
    }

    [Fact]
    public void GenerateTag_TruncatesTo32Chars()
    {
        var tag = PlaceableNamingService.GenerateTag("A Very Long Placeable Name That Exceeds Thirty Two Characters");
        Assert.True(tag.Length <= 32, $"Tag '{tag}' exceeds 32 chars");
    }

    [Fact]
    public void Generate_TrimsTrailingUnderscoreAfterTruncation()
    {
        // 16-char boundary landing on a space would otherwise leave a trailing underscore.
        var resRef = PlaceableNamingService.GenerateResRef("abcdefghijklmno pqr");
        Assert.False(resRef.EndsWith("_"), $"ResRef '{resRef}' has a trailing underscore");
    }
}
