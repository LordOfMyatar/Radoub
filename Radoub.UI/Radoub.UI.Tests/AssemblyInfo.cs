using Avalonia.Headless;
using Radoub.UI.Tests.Fixtures;
using Xunit;

// Wire the Avalonia headless test application. The Avalonia.Headless.XUnit
// test framework discovers this attribute and runs every test in this
// assembly on the Avalonia dispatcher thread, which:
//   1. Allows tests to construct Avalonia controls without "Call from invalid
//      thread" InvalidOperationException.
//   2. Seeds the process-global AvaloniaPropertyRegistry from a single known
//      thread, eliminating the cross-thread race that caused #2212.
// AvaloniaTestApp.BuildAvaloniaApp() configures a minimal FluentTheme +
// headless platform app for binding.
[assembly: AvaloniaTestApplication(typeof(AvaloniaTestApp))]

// Force every test in this assembly to run sequentially.
//
// History:
//   - #2186 (Sprint 4): assembly-level serial added because a subset of tests
//     instantiated Avalonia controls (ItemBrowserPanel ->
//     FileBrowserPanelBase.InitializeComponent), touching the process-global
//     Avalonia.AvaloniaPropertyRegistry. Parallel test classes raced on registry
//     init and threw "An item with the same key has already been added. Key:
//     Avalonia.Controls.MenuItem". Serial execution prevented test-thread races.
//   - #2212: the same error returned on ubuntu-latest CI even with parallel
//     execution disabled. Root cause: AvaloniaPropertyRegistry init is also not
//     thread-safe against background framework threads (JIT, finalizer). Fix:
//     [AvaloniaTestApplication] seeds the registry via a headless AppBuilder on
//     the dispatcher thread before any test runs, and routes test execution
//     through that thread so Avalonia control construction is always
//     dispatcher-safe.
//
// This attribute is retained as defense-in-depth: a future developer who adds
// a parallel-running test class that constructs Avalonia controls outside the
// dispatcher path will see a slow test, not a flaky one. Wall-time cost is
// small — full assembly runs in ~7s.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
