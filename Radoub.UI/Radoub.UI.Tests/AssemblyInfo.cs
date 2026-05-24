using Xunit;

// Force every test in this assembly to run sequentially. A subset of tests
// (ItemBrowserPanelBifTests.GameDataService_IsSettableAndReadable) instantiates
// Avalonia controls (ItemBrowserPanel -> FileBrowserPanelBase.InitializeComponent),
// which touches the process-global Avalonia.AvaloniaPropertyRegistry. When xUnit
// runs test classes in parallel, two classes instantiating Avalonia controls on
// different threads race on that registry and throw
// "An item with the same key has already been added. Key: Avalonia.Controls.MenuItem"
// (surfaced on PR #2213 / Sprint 4 of #2186).
//
// Mirrors the Radoub.IntegrationTests pattern (#1526). Cheap — full assembly
// runs in ~7s.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
