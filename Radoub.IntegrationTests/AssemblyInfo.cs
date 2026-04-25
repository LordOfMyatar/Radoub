using Xunit;

// Force every test in this assembly to run sequentially. FlaUI tests drive
// real GUI processes via UI Automation — desktop focus, the foreground window,
// and the UIA client are process-global resources, so concurrent execution of
// FlaUI tests in the same assembly will sabotage one another (#1526).
//
// This is the assembly-wide guard. Per-collection DisableParallelization on
// each tool's *TestCollections.cs is kept as defense in depth.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
