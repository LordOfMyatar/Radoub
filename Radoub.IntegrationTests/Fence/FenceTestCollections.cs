using Xunit;

namespace Radoub.IntegrationTests.Fence;

/// <summary>
/// Test collection definition for sequential Fence tests.
/// Fence tests must run sequentially because:
/// 1. They share log files and settings
/// 2. FlaUI can only control one application at a time
/// 3. Running in parallel causes window focus conflicts
/// </summary>
[CollectionDefinition("FenceSequential")]
public class FenceSequentialCollection : ICollectionFixture<FenceSequentialFixture>
{
}

/// <summary>
/// Fixture for Fence sequential test collection.
/// </summary>
public class FenceSequentialFixture
{
}
