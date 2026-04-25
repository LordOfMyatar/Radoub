using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Collection that serializes RadoubSettings singleton tests so the static
/// `_instance` and `_settingsDirectory` fields are not raced across parallel
/// test classes (#1526 / #2051). Both `RadoubSettingsTests` and the nested
/// `Settings/RadoubSettingsTests` carry [Collection("RadoubSettings")];
/// declaring DisableParallelization here serializes them as a unit while
/// pure-fast tests in this project keep running in parallel.
/// </summary>
[CollectionDefinition("RadoubSettings", DisableParallelization = true)]
public class RadoubSettingsCollection
{
}
