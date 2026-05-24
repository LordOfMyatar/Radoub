using Radoub.Formats.Settings;
using Radoub.TestUtilities.Helpers;

namespace Radoub.Formats.Tests.Settings;

/// <summary>
/// Collection fixture that owns the lifecycle of the process-global state
/// surrounding <see cref="RadoubSettings"/>: a per-fixture temp directory,
/// the <c>RADOUB_SETTINGS_DIR</c> environment variable, and the singleton's
/// static <c>_instance</c> + <c>_settingsDirectory</c> fields.
///
/// Constructor:
///   1. Generates a unique temp directory under <see cref="Path.GetTempPath"/>.
///   2. Sets <c>RADOUB_SETTINGS_DIR</c> to that directory so the next
///      <see cref="RadoubSettings.Instance"/> access binds to it.
///   3. Resets the singleton's static fields via reflection so subsequent
///      <c>Instance</c> access constructs a fresh instance.
///
/// <see cref="Dispose"/>:
///   1. Resets the singleton again so the next test class starts clean.
///   2. Clears the <c>RADOUB_SETTINGS_DIR</c> env var so no stale value
///      leaks into the parent process or follow-up test runs.
///   3. Deletes the temp directory (swallows IO errors — best-effort).
///
/// Wired into <c>[CollectionDefinition("RadoubSettings", DisableParallelization = true)]</c>
/// in TestCollections.cs. Resolves #2051.
/// </summary>
public class RadoubSettingsFixture : IDisposable
{
    public string TestDirectory { get; }

    public RadoubSettingsFixture()
    {
        TestDirectory = Path.Combine(
            Path.GetTempPath(),
            $"RadoubSettingsFixture_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TestDirectory);

        SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", TestDirectory);
        SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
    }

    public void Dispose()
    {
        try
        {
            SingletonTestHelper.ResetSingleton<RadoubSettings>("_instance", "_settingsDirectory");
            SingletonTestHelper.ConfigureSettingsDirectory("RADOUB_SETTINGS_DIR", null);
        }
        finally
        {
            try
            {
                if (Directory.Exists(TestDirectory))
                    Directory.Delete(TestDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup — swallow IO errors so test crashes
                // during cleanup do not mask the real failure.
            }
        }
    }
}
