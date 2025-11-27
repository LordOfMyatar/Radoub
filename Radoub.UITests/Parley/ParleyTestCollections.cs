using Xunit;

namespace Radoub.UITests.Parley;

/// <summary>
/// Collection definition for Parley tests that must run sequentially.
/// Parley shares resources like log files and settings, so tests
/// cannot run in parallel without conflicts.
/// </summary>
[CollectionDefinition("ParleySequential", DisableParallelization = true)]
public class ParleySequentialCollection
{
    // This class has no code - it's just a marker for xUnit
}
