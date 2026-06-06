using Xunit;

namespace Radoub.IntegrationTests.Reliquary;

/// <summary>
/// Sequential collection for Reliquary UI tests: FlaUI drives one app at a time and the
/// tests share log files / desktop focus, so they must not run in parallel.
/// </summary>
[CollectionDefinition("ReliquarySequential", DisableParallelization = true)]
public class ReliquarySequentialCollection : ICollectionFixture<ReliquarySequentialFixture>
{
}

/// <summary>Fixture for the Reliquary sequential test collection.</summary>
public class ReliquarySequentialFixture
{
}
