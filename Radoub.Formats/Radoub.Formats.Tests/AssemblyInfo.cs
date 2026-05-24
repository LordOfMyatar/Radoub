using Xunit;

// Force every test in this assembly to run sequentially.
//
// Radoub.Formats.Tests targets xunit.v3, which schedules test classes in
// parallel by default. RadoubSettings is a process-global singleton
// (_instance + _settingsDirectory) bound to the RADOUB_SETTINGS_DIR
// environment variable. The collection-level [CollectionDefinition(...,
// DisableParallelization = true)] from #2020 serialized the two annotated
// classes, but any other test class that transitively touches
// RadoubSettings.Instance (auto-detect on first call reads the real
// ~/Radoub directory) raced against the annotated classes.
//
// Assembly-level serialization is the only way to guarantee no test
// class touches the singleton concurrently. Cost: small — the assembly
// runs in a few seconds.
//
// Mirrors the pattern in Radoub.UI.Tests (#2186 / #2212). Resolves #2051.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
