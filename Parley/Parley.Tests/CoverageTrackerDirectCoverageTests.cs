using System;
using System.Collections.Generic;
using DialogEditor.Services;
using Xunit;

namespace DialogEditor.Tests
{
    /// <summary>
    /// Coverage for the #482 direct-child per-entry metric. The simulator already computes a
    /// full-subtree per-entry count; #482 adds a parallel DIRECT-child count so the start-entry
    /// menu can show both. These tests exercise CoverageTracker.GetCoverageStats with both the
    /// subtree map and the new direct map and assert the two metrics are reported independently.
    ///
    /// Each test uses a unique file path so the shared coverage cache does not bleed between
    /// tests, and clears that path on disposal.
    /// </summary>
    public class CoverageTrackerDirectCoverageTests : IDisposable
    {
        private readonly CoverageTracker _tracker = new();
        private readonly List<string> _paths = new();

        private string NewPath()
        {
            var p = $"test_{Guid.NewGuid():N}.dlg";
            _paths.Add(p);
            return p;
        }

        public void Dispose()
        {
            foreach (var p in _paths)
            {
                try { _tracker.ClearCoverage(p); } catch { /* best-effort */ }
            }
        }

        [Fact]
        public void DirectCoverage_CountsOnlyImmediateReplies()
        {
            var path = NewPath();
            // Entry 0 has direct replies R0, R1; subtree additionally includes R2.
            _tracker.RecordVisitedNode(path, "R0"); // direct + subtree
            _tracker.RecordVisitedNode(path, "R2"); // subtree only

            var rootEntries = new List<int> { 0 };
            var subtree = new Dictionary<int, HashSet<int>> { [0] = new() { 0, 1, 2 } };
            var direct = new Dictionary<int, HashSet<int>> { [0] = new() { 0, 1 } };

            var stats = _tracker.GetCoverageStats(path, totalReplies: 3,
                rootEntryIndices: rootEntries,
                repliesPerRootEntry: subtree,
                directRepliesPerRootEntry: direct);

            Assert.Equal("1/2", stats.GetEntryDirectCoverageText(0)); // R0 visited of {R0,R1}
            Assert.Equal("2/3", stats.GetEntryCoverageText(0));       // R0,R2 visited of {R0,R1,R2}
        }

        [Fact]
        public void DirectCoverage_DiffersFromSubtreeWhenSubtreeIsLarger()
        {
            var path = NewPath();
            var rootEntries = new List<int> { 0 };
            var subtree = new Dictionary<int, HashSet<int>> { [0] = new() { 0, 1, 2, 3, 4 } };
            var direct = new Dictionary<int, HashSet<int>> { [0] = new() { 0 } };

            var stats = _tracker.GetCoverageStats(path, totalReplies: 5,
                rootEntryIndices: rootEntries,
                repliesPerRootEntry: subtree,
                directRepliesPerRootEntry: direct);

            Assert.Equal("0/1", stats.GetEntryDirectCoverageText(0));
            Assert.Equal("0/5", stats.GetEntryCoverageText(0));
        }

        [Fact]
        public void DirectCoverage_ZeroDirectReplies_ReportsZeroOverZero()
        {
            var path = NewPath();
            var rootEntries = new List<int> { 0 };
            var subtree = new Dictionary<int, HashSet<int>> { [0] = new() { 1 } };
            var direct = new Dictionary<int, HashSet<int>> { [0] = new() };

            var stats = _tracker.GetCoverageStats(path, totalReplies: 1,
                rootEntryIndices: rootEntries,
                repliesPerRootEntry: subtree,
                directRepliesPerRootEntry: direct);

            Assert.Equal("0/0", stats.GetEntryDirectCoverageText(0));
        }

        [Fact]
        public void DirectCoverage_AllDirectVisited_CountsFully()
        {
            var path = NewPath();
            _tracker.RecordVisitedNode(path, "R0");
            _tracker.RecordVisitedNode(path, "R1");

            var rootEntries = new List<int> { 0 };
            var direct = new Dictionary<int, HashSet<int>> { [0] = new() { 0, 1 } };

            var stats = _tracker.GetCoverageStats(path, totalReplies: 2,
                rootEntryIndices: rootEntries,
                repliesPerRootEntry: new Dictionary<int, HashSet<int>> { [0] = new() { 0, 1 } },
                directRepliesPerRootEntry: direct);

            Assert.Equal("2/2", stats.GetEntryDirectCoverageText(0));
        }

        [Fact]
        public void DirectCoverage_AbsentEntry_FallsBackToZeroOverZero()
        {
            var path = NewPath();
            _tracker.RecordVisitedNode(path, "R0");

            var stats = _tracker.GetCoverageStats(path, totalReplies: 1,
                rootEntryIndices: new List<int> { 0 },
                repliesPerRootEntry: new Dictionary<int, HashSet<int>> { [0] = new() { 0 } },
                directRepliesPerRootEntry: new Dictionary<int, HashSet<int>> { [0] = new() { 0 } });

            // Entry 99 was never mapped -> default text.
            Assert.Equal("0/0", stats.GetEntryDirectCoverageText(99));
        }

        [Fact]
        public void DirectCoverage_OmittedMap_LeavesDirectEmptyButSubtreeIntact()
        {
            var path = NewPath();
            _tracker.RecordVisitedNode(path, "R0");

            // Backwards-compat: callers that don't pass the direct map still get subtree coverage.
            var stats = _tracker.GetCoverageStats(path, totalReplies: 1,
                rootEntryIndices: new List<int> { 0 },
                repliesPerRootEntry: new Dictionary<int, HashSet<int>> { [0] = new() { 0 } });

            Assert.Equal("1/1", stats.GetEntryCoverageText(0));
            Assert.Equal("0/0", stats.GetEntryDirectCoverageText(0)); // empty -> default
        }
    }
}
