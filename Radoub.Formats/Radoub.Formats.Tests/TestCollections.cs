using Radoub.Formats.Tests.Settings;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Collection that serializes RadoubSettings singleton tests so the static
/// `_instance` and `_settingsDirectory` fields plus the `RADOUB_SETTINGS_DIR`
/// environment variable are not raced across parallel test classes
/// (#1526 / #2051).
///
/// Implements <see cref="ICollectionFixture{TFixture}"/> for
/// <see cref="RadoubSettingsFixture"/> so test classes carrying
/// `[Collection("RadoubSettings")]` receive a per-collection fixture that
/// owns the env var + temp directory + singleton reset lifecycle.
///
/// Assembly-level serialization in AssemblyInfo.cs provides the outer
/// guarantee that no class outside this collection races against the
/// singleton; the collection fixture provides the inner guarantee that
/// state is reset before each class inside the collection.
/// </summary>
[CollectionDefinition("RadoubSettings", DisableParallelization = true)]
public class RadoubSettingsCollection : ICollectionFixture<RadoubSettingsFixture>
{
}
