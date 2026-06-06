// Disable test parallelization to prevent conflicts with singleton services.
// SettingsService is a process-global singleton and SingletonTestHelper sets a
// process-global env var (TREBUCHET_SETTINGS_DIR); two test classes manipulating
// both concurrently race and produce intermittent failures (#2360). Mirrors the
// Quartermaster.Tests convention.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
