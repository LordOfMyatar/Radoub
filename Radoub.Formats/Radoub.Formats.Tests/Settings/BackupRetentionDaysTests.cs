using Radoub.Formats.Settings;
using Xunit;

namespace Radoub.Formats.Tests.Settings;

[Collection("RadoubSettings")]
public class BackupRetentionDaysTests
{
    private readonly RadoubSettingsFixture _fixture;

    public BackupRetentionDaysTests(RadoubSettingsFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(0, 1)]    // Below minimum, clamps to 1
    [InlineData(-5, 1)]   // Negative, clamps to 1
    [InlineData(1, 1)]    // At minimum
    [InlineData(45, 45)]  // In range
    [InlineData(90, 90)]  // At maximum
    [InlineData(91, 90)]  // Above maximum, clamps to 90
    [InlineData(365, 90)] // Way above maximum, clamps to 90
    public void BackupRetentionDays_ClampsToRange(int input, int expected)
    {
        // Fixture binds the singleton to a fresh temp directory; touch keeps the
        // unused-variable analyzer quiet without changing semantics.
        _ = _fixture;

        var settings = RadoubSettings.Instance;
        var original = settings.BackupRetentionDays;
        try
        {
            settings.BackupRetentionDays = input;
            Assert.Equal(expected, settings.BackupRetentionDays);
        }
        finally
        {
            settings.BackupRetentionDays = original;
        }
    }
}
