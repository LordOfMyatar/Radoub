using Xunit;

namespace Radoub.IntegrationTests.Manifest;

/// <summary>
/// Collection definition for Manifest tests that must run sequentially.
/// Manifest shares resources like log files and settings, so tests
/// cannot run in parallel without conflicts.
/// </summary>
[CollectionDefinition("ManifestSequential", DisableParallelization = true)]
public class ManifestSequentialCollection
{
    // This class has no code - it's just a marker for xUnit
}
