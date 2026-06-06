using Xunit;

namespace Radoub.IntegrationTests.Relique;

/// <summary>
/// Sequential collection for Relique UI tests: FlaUI drives one app at a time and the
/// tests share log files / desktop focus, so they must not run in parallel.
/// </summary>
[CollectionDefinition("ReliqueSequential", DisableParallelization = true)]
public class ReliqueSequentialCollection : ICollectionFixture<ReliqueSequentialFixture>
{
}

/// <summary>Fixture for the Relique sequential test collection.</summary>
public class ReliqueSequentialFixture
{
}
