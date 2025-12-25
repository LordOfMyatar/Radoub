using Radoub.UITests.Shared;

namespace Radoub.UITests.Manifest;

/// <summary>
/// Base class for Manifest-specific UI tests.
/// </summary>
public abstract class ManifestTestBase : FlaUITestBase
{
    protected override string ApplicationPath => TestPaths.GetManifestExePath();
}
