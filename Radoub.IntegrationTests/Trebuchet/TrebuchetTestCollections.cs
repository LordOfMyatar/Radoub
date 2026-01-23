using Xunit;

namespace Radoub.IntegrationTests.Trebuchet;

/// <summary>
/// Test collection definition for sequential Trebuchet tests.
/// Trebuchet tests must run sequentially because:
/// 1. They share log files and settings
/// 2. FlaUI can only control one application at a time
/// 3. Running in parallel causes window focus conflicts
/// </summary>
[CollectionDefinition("TrebuchetSequential")]
public class TrebuchetSequentialCollection : ICollectionFixture<TrebuchetSequentialFixture>
{
}

/// <summary>
/// Fixture for Trebuchet sequential test collection.
/// </summary>
public class TrebuchetSequentialFixture
{
}
