using ItemEditor.Services;
using System.Collections.Generic;
using Xunit;

namespace ItemEditor.Tests.Services;

public class TreeExpansionTrackerTests
{
    [Fact]
    public void Capture_EmptyState_ReturnsEmptySnapshot()
    {
        var snapshot = TreeExpansionTracker.Capture(System.Array.Empty<int>());

        Assert.Empty(snapshot.ExpandedPropertyIndices);
    }

    [Fact]
    public void Capture_StoresAllProvidedIndices()
    {
        var snapshot = TreeExpansionTracker.Capture(new[] { 1, 7, 42 });

        Assert.Equal(new HashSet<int> { 1, 7, 42 }, snapshot.ExpandedPropertyIndices);
    }

    [Fact]
    public void ShouldExpand_ReturnsTrue_WhenIndexInSnapshot()
    {
        var snapshot = TreeExpansionTracker.Capture(new[] { 5, 9 });

        Assert.True(snapshot.ShouldExpand(5));
        Assert.True(snapshot.ShouldExpand(9));
    }

    [Fact]
    public void ShouldExpand_ReturnsFalse_WhenIndexNotInSnapshot()
    {
        var snapshot = TreeExpansionTracker.Capture(new[] { 5, 9 });

        Assert.False(snapshot.ShouldExpand(1));
        Assert.False(snapshot.ShouldExpand(100));
    }

    [Fact]
    public void Empty_NoIndicesExpand()
    {
        var snapshot = TreeExpansionSnapshot.Empty;

        Assert.False(snapshot.ShouldExpand(0));
        Assert.False(snapshot.ShouldExpand(42));
    }

    [Fact]
    public void Capture_DeduplicatesIndices()
    {
        var snapshot = TreeExpansionTracker.Capture(new[] { 3, 3, 7, 3 });

        Assert.Equal(2, snapshot.ExpandedPropertyIndices.Count);
        Assert.Contains(3, snapshot.ExpandedPropertyIndices);
        Assert.Contains(7, snapshot.ExpandedPropertyIndices);
    }

    [Fact]
    public void Capture_FromNull_ReturnsEmpty()
    {
        var snapshot = TreeExpansionTracker.Capture(null!);

        Assert.Empty(snapshot.ExpandedPropertyIndices);
    }
}
