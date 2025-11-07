using Xunit;

// Disable parallel test execution to prevent logger file access conflicts
// Tests share the same UnifiedLogger session and file paths, which causes
// file locking errors when tests run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
