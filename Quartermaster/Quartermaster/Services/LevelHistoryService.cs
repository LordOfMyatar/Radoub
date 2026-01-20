using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Radoub.Formats.Logging;

namespace Quartermaster.Services;

/// <summary>
/// Encoding format for level history in UTC comments.
/// </summary>
public enum LevelHistoryEncoding
{
    /// <summary>Human-readable text format. Default.</summary>
    Readable,
    /// <summary>Base64 binary encoding - most compact.</summary>
    Binary,
    /// <summary>Minified JSON with gzip compression.</summary>
    JsonCompressed
}

/// <summary>
/// Represents a single level-up record.
/// </summary>
public class LevelRecord
{
    public int TotalLevel { get; set; }
    public int ClassId { get; set; }
    public int ClassLevel { get; set; }
    public List<int> Feats { get; set; } = new();
    public Dictionary<int, int> Skills { get; set; } = new();
    /// <summary>Ability increased at this level (-1 if none, 0=STR, 1=DEX, 2=CON, 3=INT, 4=WIS, 5=CHA)</summary>
    public int AbilityIncrease { get; set; } = -1;
}

/// <summary>
/// Service for encoding/decoding level-up history to/from UTC comment fields.
/// Supports multiple encoding formats for different use cases.
/// </summary>
public static class LevelHistoryService
{
    private const string ReadablePrefix = "QM:";
    private const string BinaryPrefix = "QMB:";
    private const string JsonPrefix = "QMJ:";

    /// <summary>
    /// Encode a list of level records to string format.
    /// </summary>
    public static string Encode(List<LevelRecord> records, LevelHistoryEncoding encoding)
    {
        if (records == null || records.Count == 0)
            return "";

        return encoding switch
        {
            LevelHistoryEncoding.Readable => EncodeReadable(records),
            LevelHistoryEncoding.Binary => EncodeBinary(records),
            LevelHistoryEncoding.JsonCompressed => EncodeJsonCompressed(records),
            _ => EncodeReadable(records)
        };
    }

    /// <summary>
    /// Decode level history from a comment string.
    /// Auto-detects the encoding format.
    /// </summary>
    public static List<LevelRecord>? Decode(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return null;

        try
        {
            // Find our marker in the comment (may have other content)
            if (comment.Contains(ReadablePrefix))
            {
                var start = comment.IndexOf(ReadablePrefix);
                var data = ExtractHistoryBlock(comment, start);
                return DecodeReadable(data);
            }
            else if (comment.Contains(BinaryPrefix))
            {
                var start = comment.IndexOf(BinaryPrefix);
                var data = ExtractHistoryBlock(comment, start);
                return DecodeBinary(data);
            }
            else if (comment.Contains(JsonPrefix))
            {
                var start = comment.IndexOf(JsonPrefix);
                var data = ExtractHistoryBlock(comment, start);
                return DecodeJsonCompressed(data);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to decode level history: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Check if comment contains level history data.
    /// </summary>
    public static bool HasLevelHistory(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return false;

        return comment.Contains(ReadablePrefix) ||
               comment.Contains(BinaryPrefix) ||
               comment.Contains(JsonPrefix);
    }

    /// <summary>
    /// Remove level history from comment, preserving other content.
    /// </summary>
    public static string RemoveLevelHistory(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return "";

        var result = comment;

        // Remove each type of history block
        foreach (var prefix in new[] { ReadablePrefix, BinaryPrefix, JsonPrefix })
        {
            while (result.Contains(prefix))
            {
                var start = result.IndexOf(prefix);
                var end = FindHistoryEnd(result, start);
                result = result.Remove(start, end - start).Trim();
            }
        }

        return result;
    }

    /// <summary>
    /// Append level history to existing comment, replacing any existing history.
    /// </summary>
    public static string AppendToComment(string? existingComment, List<LevelRecord> records, LevelHistoryEncoding encoding)
    {
        var cleanComment = RemoveLevelHistory(existingComment);
        var history = Encode(records, encoding);

        if (string.IsNullOrEmpty(history))
            return cleanComment;

        if (string.IsNullOrEmpty(cleanComment))
            return history;

        return cleanComment + "\n\n" + history;
    }

    #region Readable Format

    // Format: QM:L1C0F12,45S0:3,5:2A-|L2C0F-S0:1A-|L4C0F88S3:2A1
    // L=TotalLevel, C=ClassId, F=Feats (comma-sep or -), S=Skills (id:ranks pairs), A=Ability (-1 as -)
    private static string EncodeReadable(List<LevelRecord> records)
    {
        var sb = new StringBuilder(ReadablePrefix);

        for (int i = 0; i < records.Count; i++)
        {
            if (i > 0) sb.Append('|');
            var r = records[i];

            sb.Append('L').Append(r.TotalLevel);
            sb.Append('C').Append(r.ClassId);
            sb.Append('V').Append(r.ClassLevel); // V for class leVel

            // Feats
            sb.Append('F');
            if (r.Feats.Count > 0)
                sb.Append(string.Join(",", r.Feats));
            else
                sb.Append('-');

            // Skills
            sb.Append('S');
            if (r.Skills.Count > 0)
                sb.Append(string.Join(",", r.Skills.Select(kv => $"{kv.Key}:{kv.Value}")));
            else
                sb.Append('-');

            // Ability
            sb.Append('A');
            sb.Append(r.AbilityIncrease >= 0 ? r.AbilityIncrease.ToString() : "-");
        }

        return sb.ToString();
    }

    private static List<LevelRecord> DecodeReadable(string data)
    {
        var records = new List<LevelRecord>();

        if (!data.StartsWith(ReadablePrefix))
            return records;

        var content = data.Substring(ReadablePrefix.Length);
        var levels = content.Split('|', StringSplitOptions.RemoveEmptyEntries);

        foreach (var level in levels)
        {
            var record = new LevelRecord();

            // Parse L (total level)
            var lIdx = level.IndexOf('L');
            var cIdx = level.IndexOf('C');
            if (lIdx >= 0 && cIdx > lIdx)
            {
                if (int.TryParse(level.Substring(lIdx + 1, cIdx - lIdx - 1), out int totalLevel))
                    record.TotalLevel = totalLevel;
            }

            // Parse C (class id)
            var vIdx = level.IndexOf('V');
            if (cIdx >= 0 && vIdx > cIdx)
            {
                if (int.TryParse(level.Substring(cIdx + 1, vIdx - cIdx - 1), out int classId))
                    record.ClassId = classId;
            }

            // Parse V (class level)
            var fIdx = level.IndexOf('F');
            if (vIdx >= 0 && fIdx > vIdx)
            {
                if (int.TryParse(level.Substring(vIdx + 1, fIdx - vIdx - 1), out int classLevel))
                    record.ClassLevel = classLevel;
            }

            // Parse F (feats)
            var sIdx = level.IndexOf('S');
            if (fIdx >= 0 && sIdx > fIdx)
            {
                var featsStr = level.Substring(fIdx + 1, sIdx - fIdx - 1);
                if (featsStr != "-")
                {
                    foreach (var f in featsStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(f, out int featId))
                            record.Feats.Add(featId);
                    }
                }
            }

            // Parse S (skills)
            var aIdx = level.IndexOf('A');
            if (sIdx >= 0 && aIdx > sIdx)
            {
                var skillsStr = level.Substring(sIdx + 1, aIdx - sIdx - 1);
                if (skillsStr != "-")
                {
                    foreach (var s in skillsStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = s.Split(':');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out int skillId) &&
                            int.TryParse(parts[1], out int ranks))
                        {
                            record.Skills[skillId] = ranks;
                        }
                    }
                }
            }

            // Parse A (ability)
            if (aIdx >= 0)
            {
                var abilityStr = level.Substring(aIdx + 1);
                if (abilityStr == "-")
                    record.AbilityIncrease = -1;
                else if (int.TryParse(abilityStr, out int ability))
                    record.AbilityIncrease = ability;
            }

            records.Add(record);
        }

        return records;
    }

    #endregion

    #region Binary Format

    // Compact binary: each level is variable length
    // Header: 1 byte version, 1 byte level count
    // Per level: TotalLevel(1), ClassId(1), ClassLevel(1), FeatCount(1), Feats(2 each), SkillCount(1), Skills(1+1 each), Ability(1)
    private static string EncodeBinary(List<LevelRecord> records)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)1); // Version
        bw.Write((byte)records.Count);

        foreach (var r in records)
        {
            bw.Write((byte)r.TotalLevel);
            bw.Write((byte)r.ClassId);
            bw.Write((byte)r.ClassLevel);

            // Feats (2 bytes each for feat IDs up to 65535)
            bw.Write((byte)Math.Min(255, r.Feats.Count));
            foreach (var f in r.Feats.Take(255))
                bw.Write((ushort)f);

            // Skills (1 byte id, 1 byte ranks - max 127 skills, max 255 ranks)
            var skillPairs = r.Skills.Take(127).ToList();
            bw.Write((byte)skillPairs.Count);
            foreach (var kv in skillPairs)
            {
                bw.Write((byte)kv.Key);
                bw.Write((byte)Math.Min(255, kv.Value));
            }

            // Ability (-1 stored as 255)
            bw.Write((byte)(r.AbilityIncrease >= 0 ? r.AbilityIncrease : 255));
        }

        return BinaryPrefix + Convert.ToBase64String(ms.ToArray());
    }

    private static List<LevelRecord> DecodeBinary(string data)
    {
        var records = new List<LevelRecord>();

        if (!data.StartsWith(BinaryPrefix))
            return records;

        var bytes = Convert.FromBase64String(data.Substring(BinaryPrefix.Length));
        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);

        var version = br.ReadByte();
        if (version != 1)
            return records; // Unsupported version

        var levelCount = br.ReadByte();

        for (int i = 0; i < levelCount && ms.Position < ms.Length; i++)
        {
            var record = new LevelRecord
            {
                TotalLevel = br.ReadByte(),
                ClassId = br.ReadByte(),
                ClassLevel = br.ReadByte()
            };

            var featCount = br.ReadByte();
            for (int f = 0; f < featCount && ms.Position < ms.Length - 1; f++)
                record.Feats.Add(br.ReadUInt16());

            var skillCount = br.ReadByte();
            for (int s = 0; s < skillCount && ms.Position < ms.Length - 1; s++)
            {
                var skillId = br.ReadByte();
                var ranks = br.ReadByte();
                record.Skills[skillId] = ranks;
            }

            var ability = br.ReadByte();
            record.AbilityIncrease = ability == 255 ? -1 : ability;

            records.Add(record);
        }

        return records;
    }

    #endregion

    #region JSON Compressed Format

    private static string EncodeJsonCompressed(List<LevelRecord> records)
    {
        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(jsonBytes, 0, jsonBytes.Length);
        }

        return JsonPrefix + Convert.ToBase64String(output.ToArray());
    }

    private static List<LevelRecord> DecodeJsonCompressed(string data)
    {
        if (!data.StartsWith(JsonPrefix))
            return new List<LevelRecord>();

        var compressed = Convert.FromBase64String(data.Substring(JsonPrefix.Length));

        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);

        var json = Encoding.UTF8.GetString(output.ToArray());
        return JsonSerializer.Deserialize<List<LevelRecord>>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) ?? new List<LevelRecord>();
    }

    #endregion

    #region Helpers

    private static string ExtractHistoryBlock(string comment, int start)
    {
        var end = FindHistoryEnd(comment, start);
        return comment.Substring(start, end - start);
    }

    private static int FindHistoryEnd(string comment, int start)
    {
        // For readable format, find next newline or end
        // For binary/json, find end of base64 (next space/newline or end)
        var end = comment.Length;

        for (int i = start + 3; i < comment.Length; i++) // Skip prefix
        {
            var c = comment[i];
            if (c == '\n' || c == '\r')
            {
                end = i;
                break;
            }
        }

        return end;
    }

    #endregion

    #region Display Helpers

    /// <summary>
    /// Format level history for display in UI.
    /// </summary>
    public static string FormatForDisplay(List<LevelRecord> records, Func<int, string> getClassName, Func<int, string> getFeatName, Func<int, string> getSkillName)
    {
        if (records == null || records.Count == 0)
            return "No level history recorded.";

        var sb = new StringBuilder();

        foreach (var r in records)
        {
            sb.AppendLine($"Level {r.TotalLevel}: {getClassName(r.ClassId)} {r.ClassLevel}");

            if (r.Feats.Count > 0)
            {
                sb.Append("  Feats: ");
                sb.AppendLine(string.Join(", ", r.Feats.Select(getFeatName)));
            }

            if (r.Skills.Count > 0)
            {
                sb.Append("  Skills: ");
                sb.AppendLine(string.Join(", ", r.Skills.Select(kv => $"{getSkillName(kv.Key)} +{kv.Value}")));
            }

            if (r.AbilityIncrease >= 0)
            {
                var abilityNames = new[] { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
                sb.AppendLine($"  Ability: +1 {abilityNames[r.AbilityIncrease]}");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Get encoding size estimates for comparison.
    /// </summary>
    public static (int readable, int binary, int json) GetEncodingSizes(List<LevelRecord> records)
    {
        return (
            EncodeReadable(records).Length,
            EncodeBinary(records).Length,
            EncodeJsonCompressed(records).Length
        );
    }

    #endregion
}
