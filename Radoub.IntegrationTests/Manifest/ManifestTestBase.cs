using Radoub.IntegrationTests.Shared;

namespace Radoub.IntegrationTests.Manifest;

/// <summary>
/// Base class for Manifest-specific UI tests.
/// </summary>
public abstract class ManifestTestBase : FlaUITestBase
{
    protected override string ApplicationPath => TestPaths.GetManifestExePath();
}
