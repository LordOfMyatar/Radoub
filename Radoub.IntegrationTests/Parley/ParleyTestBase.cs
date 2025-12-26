using Radoub.IntegrationTests.Shared;

namespace Radoub.IntegrationTests.Parley;

/// <summary>
/// Base class for Parley-specific UI tests.
/// </summary>
public abstract class ParleyTestBase : FlaUITestBase
{
    protected override string ApplicationPath => TestPaths.GetParleyExePath();
}
