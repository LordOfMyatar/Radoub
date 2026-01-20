using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for the LevelHistoryService encoding/decoding functionality.
/// </summary>
public class LevelHistoryServiceTests
{
    [Fact]
    public void EncodeDecodeReadable_RoundTrip_PreservesData()
    {
        // Arrange
        var records = CreateSampleHistory();

        // Act
        var encoded = LevelHistoryService.Encode(records, LevelHistoryEncoding.Readable);
        var decoded = LevelHistoryService.Decode(encoded);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(records.Count, decoded.Count);
        AssertRecordsEqual(records[0], decoded[0]);
        AssertRecordsEqual(records[1], decoded[1]);
    }

    [Fact]
    public void EncodeDecodeBinary_RoundTrip_PreservesData()
    {
        // Arrange
        var records = CreateSampleHistory();

        // Act
        var encoded = LevelHistoryService.Encode(records, LevelHistoryEncoding.Binary);
        var decoded = LevelHistoryService.Decode(encoded);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(records.Count, decoded.Count);
        AssertRecordsEqual(records[0], decoded[0]);
        AssertRecordsEqual(records[1], decoded[1]);
    }

    [Fact]
    public void EncodeDecodeJsonCompressed_RoundTrip_PreservesData()
    {
        // Arrange
        var records = CreateSampleHistory();

        // Act
        var encoded = LevelHistoryService.Encode(records, LevelHistoryEncoding.JsonCompressed);
        var decoded = LevelHistoryService.Decode(encoded);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(records.Count, decoded.Count);
        AssertRecordsEqual(records[0], decoded[0]);
        AssertRecordsEqual(records[1], decoded[1]);
    }

    [Fact]
    public void ReadableFormat_ProducesExpectedPrefix()
    {
        var records = new List<LevelRecord> { new LevelRecord { TotalLevel = 1, ClassId = 0 } };
        var encoded = LevelHistoryService.Encode(records, LevelHistoryEncoding.Readable);

        Assert.StartsWith("QM:", encoded);
    }

    [Fact]
    public void BinaryFormat_ProducesExpectedPrefix()
    {
        var records = new List<LevelRecord> { new LevelRecord { TotalLevel = 1, ClassId = 0 } };
        var encoded = LevelHistoryService.Encode(records, LevelHistoryEncoding.Binary);

        Assert.StartsWith("QMB:", encoded);
    }

    [Fact]
    public void JsonFormat_ProducesExpectedPrefix()
    {
        var records = new List<LevelRecord> { new LevelRecord { TotalLevel = 1, ClassId = 0 } };
        var encoded = LevelHistoryService.Encode(records, LevelHistoryEncoding.JsonCompressed);

        Assert.StartsWith("QMJ:", encoded);
    }

    [Fact]
    public void HasLevelHistory_ReturnsTrueForEncodedComment()
    {
        var records = new List<LevelRecord> { new LevelRecord { TotalLevel = 1, ClassId = 0 } };

        Assert.True(LevelHistoryService.HasLevelHistory(LevelHistoryService.Encode(records, LevelHistoryEncoding.Readable)));
        Assert.True(LevelHistoryService.HasLevelHistory(LevelHistoryService.Encode(records, LevelHistoryEncoding.Binary)));
        Assert.True(LevelHistoryService.HasLevelHistory(LevelHistoryService.Encode(records, LevelHistoryEncoding.JsonCompressed)));
    }

    [Fact]
    public void HasLevelHistory_ReturnsFalseForRegularComment()
    {
        Assert.False(LevelHistoryService.HasLevelHistory("Just a regular comment"));
        Assert.False(LevelHistoryService.HasLevelHistory(null));
        Assert.False(LevelHistoryService.HasLevelHistory(""));
    }

    [Fact]
    public void AppendToComment_PreservesExistingContent()
    {
        // Arrange
        var existingComment = "This is my character notes.";
        var records = new List<LevelRecord> { new LevelRecord { TotalLevel = 1, ClassId = 0 } };

        // Act
        var result = LevelHistoryService.AppendToComment(existingComment, records, LevelHistoryEncoding.Readable);

        // Assert
        Assert.Contains(existingComment, result);
        Assert.Contains("QM:", result);
    }

    [Fact]
    public void AppendToComment_ReplacesExistingHistory()
    {
        // Arrange
        var oldRecords = new List<LevelRecord> { new LevelRecord { TotalLevel = 1, ClassId = 0 } };
        var commentWithHistory = LevelHistoryService.Encode(oldRecords, LevelHistoryEncoding.Readable);

        var newRecords = new List<LevelRecord>
        {
            new LevelRecord { TotalLevel = 1, ClassId = 0 },
            new LevelRecord { TotalLevel = 2, ClassId = 0 }
        };

        // Act
        var result = LevelHistoryService.AppendToComment(commentWithHistory, newRecords, LevelHistoryEncoding.Readable);

        // Assert
        var decoded = LevelHistoryService.Decode(result);
        Assert.NotNull(decoded);
        Assert.Equal(2, decoded.Count); // Should have new history, not old + new
    }

    [Fact]
    public void RemoveLevelHistory_RemovesEncodedBlock()
    {
        // Arrange
        var comment = "Notes before.\n\nQM:L1C0F-S-A-|L2C0F-S-A-\n\nNotes after.";

        // Act
        var result = LevelHistoryService.RemoveLevelHistory(comment);

        // Assert
        Assert.DoesNotContain("QM:", result);
        Assert.Contains("Notes before", result);
        Assert.Contains("Notes after", result);
    }

    [Fact]
    public void BinaryFormat_MoreCompactThanReadable()
    {
        // Arrange - create a 10-level history with feats and skills
        var records = new List<LevelRecord>();
        for (int i = 1; i <= 10; i++)
        {
            records.Add(new LevelRecord
            {
                TotalLevel = i,
                ClassId = 0,
                ClassLevel = i,
                Feats = i % 3 == 0 ? new List<int> { 10 + i, 20 + i } : new List<int>(),
                Skills = new Dictionary<int, int> { { 0, 1 }, { 5, 1 }, { 12, 1 } },
                AbilityIncrease = i % 4 == 0 ? 0 : -1
            });
        }

        // Act
        var sizes = LevelHistoryService.GetEncodingSizes(records);

        // Assert
        Assert.True(sizes.binary < sizes.readable, $"Binary ({sizes.binary}) should be smaller than Readable ({sizes.readable})");
    }

    private static List<LevelRecord> CreateSampleHistory()
    {
        return new List<LevelRecord>
        {
            new LevelRecord
            {
                TotalLevel = 1,
                ClassId = 0,
                ClassLevel = 1,
                Feats = new List<int> { 12, 45 },
                Skills = new Dictionary<int, int> { { 0, 3 }, { 5, 2 }, { 12, 4 } },
                AbilityIncrease = -1
            },
            new LevelRecord
            {
                TotalLevel = 2,
                ClassId = 0,
                ClassLevel = 2,
                Feats = new List<int>(),
                Skills = new Dictionary<int, int> { { 0, 1 }, { 5, 1 } },
                AbilityIncrease = -1
            }
        };
    }

    private static void AssertRecordsEqual(LevelRecord expected, LevelRecord actual)
    {
        Assert.Equal(expected.TotalLevel, actual.TotalLevel);
        Assert.Equal(expected.ClassId, actual.ClassId);
        Assert.Equal(expected.ClassLevel, actual.ClassLevel);
        Assert.Equal(expected.AbilityIncrease, actual.AbilityIncrease);
        Assert.Equal(expected.Feats.Count, actual.Feats.Count);
        for (int i = 0; i < expected.Feats.Count; i++)
            Assert.Equal(expected.Feats[i], actual.Feats[i]);
        Assert.Equal(expected.Skills.Count, actual.Skills.Count);
        foreach (var kv in expected.Skills)
            Assert.Equal(kv.Value, actual.Skills[kv.Key]);
    }
}
