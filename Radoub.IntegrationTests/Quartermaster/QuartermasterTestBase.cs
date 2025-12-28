using Radoub.IntegrationTests.Shared;

namespace Radoub.IntegrationTests.Quartermaster;

/// <summary>
/// Base class for Quartermaster UI tests.
/// </summary>
public abstract class QuartermasterTestBase : FlaUITestBase
{
    protected override string ApplicationPath => TestPaths.GetQuartermasterExePath();
}
