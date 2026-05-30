using Manifest.Views;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Tests for MainWindow.EditOps pure logic methods.
/// </summary>
public class EditOpsTests
{
    #region GenerateUniqueTag

    [Fact]
    public void GenerateUniqueTag_EmptySet_ReturnsBaseTag()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = MainWindow.GenerateUniqueTag("new_category", existing);

        Assert.Equal("new_category", result);
    }

    [Fact]
    public void GenerateUniqueTag_BaseTagTaken_ReturnsSuffix001()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "new_category" };

        var result = MainWindow.GenerateUniqueTag("new_category", existing);

        Assert.Equal("new_category_001", result);
    }

    [Fact]
    public void GenerateUniqueTag_MultipleTaken_ReturnsNextAvailable()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "new_category",
            "new_category_001",
            "new_category_002"
        };

        var result = MainWindow.GenerateUniqueTag("new_category", existing);

        Assert.Equal("new_category_003", result);
    }

    [Fact]
    public void GenerateUniqueTag_CaseInsensitive_AvoidsDuplicate()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NEW_CATEGORY" };

        var result = MainWindow.GenerateUniqueTag("new_category", existing);

        // Base tag collides case-insensitively, so should get _001
        Assert.Equal("new_category_001", result);
    }

    [Fact]
    public void GenerateUniqueTag_GapInSuffixes_FillsGap()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "quest",
            "quest_001",
            // quest_002 is missing
            "quest_003"
        };

        var result = MainWindow.GenerateUniqueTag("quest", existing);

        Assert.Equal("quest_002", result);
    }

    [Fact]
    public void GenerateUniqueTag_ThreeDigitPadding_FormatsCorrectly()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tag" };

        var result = MainWindow.GenerateUniqueTag("tag", existing);

        // Should be zero-padded to 3 digits
        Assert.Equal("tag_001", result);
    }

    #endregion

    #region TryMutateWithRollback (#2254)

    [Fact]
    public void TryMutateWithRollback_RefreshSucceeds_KeepsItemAndReturnsTrue()
    {
        var list = new List<string> { "a", "b" };
        var refreshed = false;

        var result = MainWindow.TryMutateWithRollback(list, "c", () => refreshed = true);

        Assert.True(result);
        Assert.True(refreshed);
        Assert.Equal(new[] { "a", "b", "c" }, list);
    }

    [Fact]
    public void TryMutateWithRollback_RefreshThrows_RemovesItemAndReturnsFalse()
    {
        var list = new List<string> { "a", "b" };

        var result = MainWindow.TryMutateWithRollback(
            list, "c", () => throw new InvalidOperationException("refresh blew up"));

        Assert.False(result);
        // Model rolled back to pre-add state — the half-added item is gone.
        Assert.Equal(new[] { "a", "b" }, list);
    }

    [Fact]
    public void TryMutateWithRollback_RefreshThrows_OnlyRemovesTheAddedItem()
    {
        // Rollback must remove the specific item it added, not blindly the last element.
        var list = new List<string> { "a", "b" };

        MainWindow.TryMutateWithRollback(
            list, "c", () => throw new InvalidOperationException());

        // "b" (a pre-existing last element) must survive.
        Assert.Contains("b", list);
        Assert.DoesNotContain("c", list);
    }

    #endregion
}
