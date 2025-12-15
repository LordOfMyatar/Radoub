using Xunit;

// Disable parallel test execution to prevent file access conflicts
// Tests may share logger files and temp directories.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
