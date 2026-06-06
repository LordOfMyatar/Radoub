using Radoub.IntegrationTests.Shared;

namespace Radoub.IntegrationTests.Relique;

/// <summary>
/// Base class for Relique (item blueprint editor) UI tests.
/// </summary>
public abstract class ReliqueTestBase : FlaUITestBase
{
    protected override string ApplicationPath => TestPaths.GetReliqueExePath();
}
