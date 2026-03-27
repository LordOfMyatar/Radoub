using Radoub.Formats.Settings;
using Xunit;

namespace Radoub.Formats.Tests.Settings;

public class BackupRetentionDaysTests
{
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
