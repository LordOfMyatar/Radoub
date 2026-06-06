using Radoub.IntegrationTests.Shared;

namespace Radoub.IntegrationTests.Reliquary;

/// <summary>
/// Base class for Reliquary (placeable editor) UI tests.
/// </summary>
public abstract class ReliquaryTestBase : FlaUITestBase
{
    protected override string ApplicationPath => TestPaths.GetReliquaryExePath();
}
