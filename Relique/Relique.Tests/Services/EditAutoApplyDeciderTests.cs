using ItemEditor.Services;
using Xunit;

namespace ItemEditor.Tests.Services;

public class EditAutoApplyDeciderTests
{
    [Fact]
    public void ShouldAutoApply_ReturnsFalse_WhenNotInEditMode()
    {
        var result = EditAutoApplyDecider.ShouldAutoApply(
            editingPropertyIndex: -1, suppressAutoApply: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldAutoApply_ReturnsTrue_WhenInEditModeAndNotSuppressed()
    {
        var result = EditAutoApplyDecider.ShouldAutoApply(
            editingPropertyIndex: 3, suppressAutoApply: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldAutoApply_ReturnsFalse_WhenSuppressed()
    {
        var result = EditAutoApplyDecider.ShouldAutoApply(
            editingPropertyIndex: 3, suppressAutoApply: true);

        Assert.False(result);
    }

    [Fact]
    public void ShouldAutoApply_ReturnsTrue_WhenEditingIndexZero()
    {
        // Boundary: index 0 is a valid editing target.
        var result = EditAutoApplyDecider.ShouldAutoApply(
            editingPropertyIndex: 0, suppressAutoApply: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData(-100, false, false)]
    [InlineData(-1, false, false)]
    [InlineData(0, true, false)]
    [InlineData(5, true, false)]
    [InlineData(5, false, true)]
    public void ShouldAutoApply_Matrix(int index, bool suppress, bool expected)
    {
        Assert.Equal(expected, EditAutoApplyDecider.ShouldAutoApply(index, suppress));
    }
}
