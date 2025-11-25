using Xunit;

namespace DialogEditor.Tests
{
    /// <summary>
    /// Collection definition for tests that share the static UnifiedLogger callback.
    /// Tests in this collection run sequentially, not in parallel.
    /// </summary>
    [CollectionDefinition("UnifiedLogger")]
    public class UnifiedLoggerCollection : ICollectionFixture<UnifiedLoggerFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// Shared fixture for UnifiedLogger tests. Currently empty but can hold
    /// shared setup/teardown logic if needed.
    /// </summary>
    public class UnifiedLoggerFixture
    {
        // Shared state or setup can go here if needed
    }
}
