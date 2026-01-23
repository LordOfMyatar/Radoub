using Radoub.IntegrationTests.Shared;

namespace Radoub.IntegrationTests.Fence;

/// <summary>
/// Base class for Fence UI tests.
/// </summary>
public abstract class FenceTestBase : FlaUITestBase
{
    protected override string ApplicationPath => TestPaths.GetFenceExePath();
}
