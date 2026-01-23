using Radoub.IntegrationTests.Shared;

namespace Radoub.IntegrationTests.Trebuchet;

/// <summary>
/// Base class for Trebuchet UI tests.
/// </summary>
public abstract class TrebuchetTestBase : FlaUITestBase
{
    protected override string ApplicationPath => TestPaths.GetTrebuchetExePath();
}
